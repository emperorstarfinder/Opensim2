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
using System.Diagnostics;
using System.Linq;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework.Monitoring.Interfaces;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    ///     Collects sim statistics which aren't
    ///      already being collected for the linden 
    ///     viewer's statistics pane
    /// </summary>
    public class SimExtraStatsCollector : BaseStatsCollector
    {
        private volatile float timeDilation;
        private volatile float simFps;
        private volatile float physicsFps;
        private volatile float agentUpdates;
        private volatile float rootAgents;
        private volatile float childAgents;
        private volatile float totalPrims;
        private volatile float activePrims;
        private volatile float totalFrameTime;
        private volatile float netFrameTime;
        private volatile float physicsFrameTime;
        private volatile float otherFrameTime;
        private volatile float imageFrameTime;
        private volatile float inPacketsPerSecond;
        private volatile float outPacketsPerSecond;
        private volatile float unackedBytes;
        private volatile float agentFrameTime;
        private volatile float pendingDownloads;
        private volatile float pendingUploads;
        private volatile float activeScripts;
        private volatile float spareTime;
        private volatile float sleepTime;
        private volatile float physicsStep;

        private volatile float scriptLinesPerSecond;
        private volatile float m_frameDilation;
        private volatile float m_usersLoggingIn;
        private volatile float m_totalGeoPrims;
        private volatile float m_totalMeshes;
        private volatile float m_inUseThreads;

        public float TimeDilation { get { return timeDilation; } }
        public float SimFps { get { return simFps; } }
        public float PhysicsFps { get { return physicsFps; } }
        public float AgentUpdates { get { return agentUpdates; } }
        public float RootAgents { get { return rootAgents; } }
        public float ChildAgents { get { return childAgents; } }
        public float TotalPrims { get { return totalPrims; } }
        public float ActivePrims { get { return activePrims; } }
        public float TotalFrameTime { get { return totalFrameTime; } }
        public float NetFrameTime { get { return netFrameTime; } }
        public float PhysicsFrameTime { get { return physicsFrameTime; } }
        public float OtherFrameTime { get { return otherFrameTime; } }
        public float ImageFrameTime { get { return imageFrameTime; } }
        public float InPacketsPerSecond { get { return inPacketsPerSecond; } }
        public float OutPacketsPerSecond { get { return outPacketsPerSecond; } }
        public float UnackedBytes { get { return unackedBytes; } }
        public float AgentFrameTime { get { return agentFrameTime; } }
        public float PendingDownloads { get { return pendingDownloads; } }
        public float PendingUploads { get { return pendingUploads; } }
        public float ActiveScripts { get { return activeScripts; } }
        public float ScriptLinesPerSecond { get { return scriptLinesPerSecond; } }

        /// <summary>
        /// Retain a dictionary of all packet queues stats reporters
        /// </summary>
        private IDictionary<UUID, PacketQueueStatsCollector> packetQueueStatsCollectors
            = new Dictionary<UUID, PacketQueueStatsCollector>();

        /// <summary>
        /// Register as a packet queue stats provider
        /// </summary>
        /// <param name="uuid">An agent UUID</param>
        /// <param name="provider"></param>
        public void RegisterPacketQueueStatsProvider(UUID uuid, IPullStatsProvider provider)
        {
            lock (packetQueueStatsCollectors)
            {
                // FIXME: If the region service is providing more than one region, then the child and root agent
                // queues are wrongly replacing each other here.
                packetQueueStatsCollectors[uuid] = new PacketQueueStatsCollector(provider);
            }
        }

        /// <summary>
        /// Deregister a packet queue stats provider
        /// </summary>
        /// <param name="uuid">An agent UUID</param>
        public void DeregisterPacketQueueStatsProvider(UUID uuid)
        {
            lock (packetQueueStatsCollectors)
            {
                packetQueueStatsCollectors.Remove(uuid);
            }
        }

        /// <summary>
        /// This is the method on which the classic sim stats reporter (which collects stats for
        /// client purposes) sends information to listeners.
        /// </summary>
        /// <param name="pack"></param>
        public void ReceiveClassicSimStatsPacket(SimStats stats)
        {
            // FIXME: SimStats shouldn't allow an arbitrary stat packing order (which is inherited from the original
            // SimStatsPacket that was being used).

            // For an unknown reason the original designers decided not to
            // include the spare MS statistic inside of this class, this is
            // located inside the StatsBlock at location 21, thus it is skipped
            timeDilation = stats.StatsBlock[0].StatValue;
            simFps = stats.StatsBlock[1].StatValue;
            physicsFps = stats.StatsBlock[2].StatValue;
            agentUpdates = stats.StatsBlock[3].StatValue;
            rootAgents = stats.StatsBlock[4].StatValue;
            childAgents = stats.StatsBlock[5].StatValue;
            totalPrims = stats.StatsBlock[6].StatValue;
            activePrims = stats.StatsBlock[7].StatValue;
            totalFrameTime = stats.StatsBlock[8].StatValue;
            netFrameTime = stats.StatsBlock[9].StatValue;
            physicsFrameTime = stats.StatsBlock[10].StatValue;
            imageFrameTime = stats.StatsBlock[11].StatValue;
            otherFrameTime = stats.StatsBlock[12].StatValue;
            inPacketsPerSecond = stats.StatsBlock[13].StatValue;
            outPacketsPerSecond = stats.StatsBlock[14].StatValue;
            unackedBytes = stats.StatsBlock[15].StatValue;
            agentFrameTime = stats.StatsBlock[16].StatValue;
            pendingDownloads = stats.StatsBlock[17].StatValue;
            pendingUploads = stats.StatsBlock[18].StatValue;
            activeScripts = stats.StatsBlock[19].StatValue;
            sleepTime = stats.StatsBlock[20].StatValue;
            spareTime = stats.StatsBlock[21].StatValue;
            physicsStep = stats.StatsBlock[22].StatValue;

            scriptLinesPerSecond = stats.ExtraStatsBlock[0].StatValue;
            m_frameDilation = stats.ExtraStatsBlock[1].StatValue;
            m_usersLoggingIn = stats.ExtraStatsBlock[2].StatValue;
            m_totalGeoPrims = stats.ExtraStatsBlock[3].StatValue;
            m_totalMeshes = stats.ExtraStatsBlock[4].StatValue;
            m_inUseThreads = stats.ExtraStatsBlock[5].StatValue;
        }

        /// <summary>
        /// Report back collected statistical information.
        /// </summary>
        /// <returns></returns>
        public override string Report()
        {
            StringBuilder sb = new StringBuilder(Environment.NewLine);

            sb.Append(Environment.NewLine);
            sb.Append("CONNECTION STATISTICS");
            sb.Append(Environment.NewLine);

            List<Stat> stats = StatsManager.GetStatsFromEachContainer("clientstack", "ClientLogoutsDueToNoReceives");

            sb.AppendFormat(
                "Client logouts due to no data receive timeout: {0}\n\n",
                stats != null ? stats.Sum(s => s.Value).ToString() : "unknown");

            sb.Append(Environment.NewLine);
            sb.Append("SAMPLE FRAME STATISTICS");
            sb.Append(Environment.NewLine);
            sb.Append("Dilatn  SimFPS  PhyFPS  AgntUp  RootAg  ChldAg  Prims   AtvPrm  AtvScr  ScrLPS");
            sb.Append(Environment.NewLine);
            sb.Append(
                string.Format(
                    "{0,6:0.00}  {1,6:0}  {2,6:0.0}  {3,6:0.0}  {4,6:0}  {5,6:0}  {6,6:0}  {7,6:0}  {8,6:0}  {9,6:0}",
                    timeDilation, simFps, physicsFps, agentUpdates, rootAgents,
                    childAgents, totalPrims, activePrims, activeScripts, scriptLinesPerSecond));

            sb.Append(Environment.NewLine);
            sb.Append(Environment.NewLine);

            // There is no script frame time currently because we don't yet collect it
            sb.Append("PktsIn  PktOut  PendDl  PendUl  UnackB  TotlFt  NetFt   PhysFt  OthrFt  AgntFt  ImgsFt");
            sb.Append(Environment.NewLine);
            sb.Append(
                string.Format(
                    "{0,6:0}  {1,6:0}  {2,6:0}  {3,6:0}  {4,6:0}  {5,6:0.0}  {6,6:0.0}  {7,6:0.0}  {8,6:0.0}  {9,6:0.0}  {10,6:0.0}\n\n",
                    inPacketsPerSecond, outPacketsPerSecond, pendingDownloads, pendingUploads, unackedBytes, totalFrameTime,
                    netFrameTime, physicsFrameTime, otherFrameTime, agentFrameTime, imageFrameTime));

            sb.Append(base.Report());

            return sb.ToString();
        }

        /// <summary>
        /// Report back collected statistical information as json serialization.
        /// </summary>
        /// <returns></returns>
        public override string XReport(string uptime, string version)
        {
            return OSDParser.SerializeJsonString(OReport(uptime, version));
        }

        /// <summary>
        /// Report back collected statistical information as an OSDMap
        /// </summary>
        /// <returns></returns>
        public override OSDMap OReport(string uptime, string version)
        {
            // Get the amount of physical memory, allocated with the instance of this program, in kilobytes;
            // the working set is the set of memory pages currently visible to this program in physical RAM
            // memory and includes both shared (e.g. system libraries) and private data
            double memUsage = Process.GetCurrentProcess().WorkingSet64 / 1024.0;

            // Get the number of threads from the system that are currently
            // running
            int numberThreadsRunning = 0;
            foreach (ProcessThread currentThread in Process.GetCurrentProcess().Threads)
            {
                // A known issue with the current process .Threads property is
                // that it can return null threads, thus don't count those as
                // running threads and prevent the program function from failing
                if (currentThread != null && currentThread.ThreadState == ThreadState.Running)
                {
                    numberThreadsRunning++;
                }
            }

            OSDMap args = new OSDMap(30);
            args["Dilatn"] = OSD.FromString(String.Format("{0:0.##}", timeDilation));
            args["SimFPS"] = OSD.FromString(String.Format("{0:0.##}", simFps));
            args["PhyFPS"] = OSD.FromString(String.Format("{0:0.##}", physicsFps));
            args["AgntUp"] = OSD.FromString(String.Format("{0:0.##}", agentUpdates));
            args["RootAg"] = OSD.FromString(String.Format("{0:0.##}", rootAgents));
            args["ChldAg"] = OSD.FromString(String.Format("{0:0.##}", childAgents));
            args["Prims"] = OSD.FromString(String.Format("{0:0.##}", totalPrims));
            args["AtvPrm"] = OSD.FromString(String.Format("{0:0.##}", activePrims));
            args["AtvScr"] = OSD.FromString(String.Format("{0:0.##}", activeScripts));
            args["ScrLPS"] = OSD.FromString(String.Format("{0:0.##}", scriptLinesPerSecond));
            args["PktsIn"] = OSD.FromString(String.Format("{0:0.##}", inPacketsPerSecond));
            args["PktOut"] = OSD.FromString(String.Format("{0:0.##}", outPacketsPerSecond));
            args["PendDl"] = OSD.FromString(String.Format("{0:0.##}", pendingDownloads));
            args["PendUl"] = OSD.FromString(String.Format("{0:0.##}", pendingUploads));
            args["UnackB"] = OSD.FromString(String.Format("{0:0.##}", unackedBytes));
            args["TotlFt"] = OSD.FromString(String.Format("{0:0.##}", totalFrameTime));
            args["NetFt"] = OSD.FromString(String.Format("{0:0.##}", netFrameTime));
            args["PhysFt"] = OSD.FromString(String.Format("{0:0.##}", physicsFrameTime));
            args["OthrFt"] = OSD.FromString(String.Format("{0:0.##}", otherFrameTime));
            args["AgntFt"] = OSD.FromString(String.Format("{0:0.##}", agentFrameTime));
            args["ImgsFt"] = OSD.FromString(String.Format("{0:0.##}", imageFrameTime));
            args["Memory"] = OSD.FromString(base.XReport(uptime, version));
            args["Uptime"] = OSD.FromString(uptime);
            args["Version"] = OSD.FromString(version);

            args["FrameDilatn"] = OSD.FromString(String.Format("{0:0.##}", m_frameDilation));
            args["Logging in Users"] = OSD.FromString(String.Format("{0:0.##}",
                m_usersLoggingIn));
            args["GeoPrims"] = OSD.FromString(String.Format("{0:0.##}",
                m_totalGeoPrims));
            args["Mesh Objects"] = OSD.FromString(String.Format("{0:0.##}",
                m_totalMeshes));
            args["XEngine Thread Count"] = OSD.FromString(String.Format("{0:0.##}",
                m_inUseThreads));
            args["Util Thread Count"] = OSD.FromString(String.Format("{0:0.##}",
                Util.GetSmartThreadPoolInfo().InUseThreads));
            args["System Thread Count"] = OSD.FromString(String.Format(
                "{0:0.##}", numberThreadsRunning));
            args["ProcMem"] = OSD.FromString(String.Format("{0:#,###,###.##}",
                memUsage));

            return args;
        }
    }

    /// <summary>
    /// Pull packet queue stats from packet queues and report
    /// </summary>
    public class PacketQueueStatsCollector : IStatsCollector
    {
        private IPullStatsProvider m_statsProvider;

        public PacketQueueStatsCollector(IPullStatsProvider provider)
        {
            m_statsProvider = provider;
        }

        /// <summary>
        /// Report back collected statistical information.
        /// </summary>
        /// <returns></returns>
        public string Report()
        {
            return m_statsProvider.GetStats();
        }

        public string XReport(string uptime, string version)
        {
            return "";
        }

        public OSDMap OReport(string uptime, string version)
        {
            OSDMap ret = new OSDMap();
            return ret;
        }
    }
}
