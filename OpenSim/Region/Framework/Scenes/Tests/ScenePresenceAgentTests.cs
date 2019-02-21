/// <license>
///     Copyright (c) Contributors, http://opensimulator.org/
///     See CONTRIBUTORS.TXT for a full list of copyright holders.
///     For an explanation of the license of each contributor and the content it
///     covers please see the Licenses directory.
///
///     Redistribution and use in source and binary forms, with or without
///     modification, are permitted provided that the following conditions are met:
///         * Redistributions of source code must retain the above copyright
///         notice, this list of conditions and the following disclaimer.
///         * Redistributions in binary form must reproduce the above copyright
///         notice, this list of conditions and the following disclaimer in the
///         documentation and/or other materials provided with the distribution.
///         * Neither the name of the OpenSimulator Project nor the
///         names of its contributors may be used to endorse or promote products
///         derived from this software without specific prior written permission.
///
///     THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
///     EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
///     WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
///     DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
///     DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
///     (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
///     LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
///     ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
///     (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
///     SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
/// </license>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ClientStack.Linden;
using OpenSim.Region.CoreModules.Framework.EntityTransfer;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using Timer = System.Timers.Timer;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    ///     Scene presence tests
    /// </summary>
    [TestFixture]
    public class ScenePresenceAgentTests : OpenSimTestCase
    {
        [Test]
        public void TestCreateRootScenePresence()
        {
            TestHelpers.InMethod();

            UUID spUuid = TestHelpers.ParseTail(0x1);

            TestScene scene = new SceneHelpers().SetupScene();
            SceneHelpers.AddScenePresence(scene, spUuid);

            Assert.That(scene.AuthenticateHandler.GetAgentCircuitData(spUuid), Is.Not.Null);
            Assert.That(scene.AuthenticateHandler.GetAgentCircuits().Count, Is.EqualTo(1));

            ScenePresence sp = scene.GetScenePresence(spUuid);
            Assert.That(sp, Is.Not.Null);
            Assert.That(sp.IsChildAgent, Is.False);
            Assert.That(sp.UUID, Is.EqualTo(spUuid));

            Assert.That(scene.GetScenePresences().Count, Is.EqualTo(1));
        }

        /// <summary>
        ///     Test that duplicate complete movement calls are ignored.
        /// </summary>
        /// <remarks>
        ///     If duplicate calls are not ignored then
        ///     there is a risk of race conditions or other 
        ///     unexpected effects.
        /// </remarks>
        [Test]
        public void TestDupeCompleteMovementCalls()
        {
            TestHelpers.InMethod();
            UUID spUuid = TestHelpers.ParseTail(0x1);

            TestScene scene = new SceneHelpers().SetupScene();

            int makeRootAgentEvents = 0;
            scene.EventManager.OnMakeRootAgent += spi => makeRootAgentEvents++;

            ScenePresence sp = SceneHelpers.AddScenePresence(scene, spUuid);

            Assert.That(makeRootAgentEvents, Is.EqualTo(1));

            // Normally these would be invoked by a CompleteMovement message coming in to the UDP stack.  But for
            // convenience, here we will invoke it manually.
            sp.CompleteMovement(sp.ControllingClient, true);

            Assert.That(makeRootAgentEvents, Is.EqualTo(1));

            // Check rest of exepcted parameters.
            Assert.That(scene.AuthenticateHandler.GetAgentCircuitData(spUuid), Is.Not.Null);
            Assert.That(scene.AuthenticateHandler.GetAgentCircuits().Count, Is.EqualTo(1));

            Assert.That(sp.IsChildAgent, Is.False);
            Assert.That(sp.UUID, Is.EqualTo(spUuid));

            Assert.That(scene.GetScenePresences().Count, Is.EqualTo(1));
        }

        [Test]
        public void TestCreateDuplicateRootScenePresence()
        {
            TestHelpers.InMethod();

            UUID spUuid = TestHelpers.ParseTail(0x1);

            // The etm is only invoked by this test to check whether an agent is still in transit if there is a dupe
            EntityTransferModule etm = new EntityTransferModule();

            IConfigSource config = new IniConfigSource();
            IConfig modulesConfig = config.AddConfig("Modules");
            modulesConfig.Set("EntityTransferModule", etm.Name);
            IConfig entityTransferConfig = config.AddConfig("EntityTransfer");

            // In order to run a single threaded regression test we do not want the entity transfer module waiting
            // for a callback from the destination scene before removing its avatar data.
            entityTransferConfig.Set("wait_for_callback", false);

            TestScene scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(scene, config, etm);
            SceneHelpers.AddScenePresence(scene, spUuid);
            SceneHelpers.AddScenePresence(scene, spUuid);

            Assert.That(scene.AuthenticateHandler.GetAgentCircuitData(spUuid), Is.Not.Null);
            Assert.That(scene.AuthenticateHandler.GetAgentCircuits().Count, Is.EqualTo(1));

            ScenePresence sp = scene.GetScenePresence(spUuid);
            Assert.That(sp, Is.Not.Null);
            Assert.That(sp.IsChildAgent, Is.False);
            Assert.That(sp.UUID, Is.EqualTo(spUuid));
        }

        [Test]
        public void TestCloseClient()
        {
            TestHelpers.InMethod();

            TestScene scene = new SceneHelpers().SetupScene();
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x1));

            scene.CloseAgent(sp.UUID, false);

            Assert.That(scene.GetScenePresence(sp.UUID), Is.Null);
            Assert.That(scene.AuthenticateHandler.GetAgentCircuitData(sp.UUID), Is.Null);
            Assert.That(scene.AuthenticateHandler.GetAgentCircuits().Count, Is.EqualTo(0));
        }

        [Test]
        public void TestCreateChildScenePresence()
        {
            TestHelpers.InMethod();

            LocalSimulationConnectorModule lsc = new LocalSimulationConnectorModule();

            IConfigSource configSource = new IniConfigSource();
            IConfig config = configSource.AddConfig("Modules");
            config.Set("SimulationServices", "LocalSimulationConnectorModule");

            SceneHelpers sceneHelpers = new SceneHelpers();
            TestScene scene = sceneHelpers.SetupScene();
            SceneHelpers.SetupSceneModules(scene, configSource, lsc);

            UUID agentId = TestHelpers.ParseTail(0x01);
            AgentCircuitData acd = SceneHelpers.GenerateAgentData(agentId);
            acd.child = true;

            GridRegion region = scene.GridService.GetRegionByName(UUID.Zero, scene.RegionInfo.RegionName);
            string reason;

            // *** This is the first stage, when a neighbouring region is told that a viewer is about to try and
            // establish a child scene presence.  We pass in the circuit code that the client has to connect with ***
            // ViaLogin may not be correct here.
            EntityTransferContext ctx = new EntityTransferContext();
            scene.SimulationService.CreateAgent(null, region, acd, (uint)TeleportFlags.ViaLogin, ctx, out reason);

            Assert.That(scene.AuthenticateHandler.GetAgentCircuitData(agentId), Is.Not.Null);
            Assert.That(scene.AuthenticateHandler.GetAgentCircuits().Count, Is.EqualTo(1));

            // There's no scene presence yet since only an agent circuit has been established.
            Assert.That(scene.GetScenePresence(agentId), Is.Null);

            // *** This is the second stage, where the client established a child agent/scene presence using the
            // circuit code given to the scene in stage 1 ***
            TestClient client = new TestClient(acd, scene);
            scene.AddNewAgent(client, PresenceType.User);

            Assert.That(scene.AuthenticateHandler.GetAgentCircuitData(agentId), Is.Not.Null);
            Assert.That(scene.AuthenticateHandler.GetAgentCircuits().Count, Is.EqualTo(1));

            ScenePresence sp = scene.GetScenePresence(agentId);
            Assert.That(sp, Is.Not.Null);
            Assert.That(sp.UUID, Is.EqualTo(agentId));
            Assert.That(sp.IsChildAgent, Is.True);
        }

        /// <summary>
        ///     Test that if a root agent logs into a region,
        ///     a child agent is also established in the neighbouring region
        /// </summary>
        /// <remarks>
        ///     Please note that unlike the other tests here, 
        ///     this doesn't rely on anything set up in the instance fields.
        ///     INCOMPLETE
        /// </remarks>
        [Test]
        public void TestChildAgentEstablishedInNeighbour()
        {
            TestHelpers.InMethod();

            TestScene myScene1 = new SceneHelpers().SetupScene("Neighbour y", UUID.Random(), 1000, 1000);
            TestScene myScene2 = new SceneHelpers().SetupScene("Neighbour y + 1", UUID.Random(), 1001, 1000);

            IConfigSource configSource = new IniConfigSource();
            IConfig config = configSource.AddConfig("Startup");
            config.Set("serverside_object_permissions", true);

            EntityTransferModule etm = new EntityTransferModule();

            EventQueueGetModule eqgm1 = new EventQueueGetModule();
            SceneHelpers.SetupSceneModules(myScene1, configSource, etm, eqgm1);

            EventQueueGetModule eqgm2 = new EventQueueGetModule();
            SceneHelpers.SetupSceneModules(myScene2, configSource, etm, eqgm2);
        }
    }
}
