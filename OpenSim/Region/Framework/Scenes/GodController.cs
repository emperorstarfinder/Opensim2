

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Timers;
using System.Xml;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Types;
using OpenSim.Services.Interfaces;
using Timer = System.Timers.Timer;

namespace OpenSim.Region.Framework.Scenes
{
    public class GodController
    {
        public enum ImplicitGodLevels : int
        {
            EstateManager = 210,    // estate manager implicit god level
            RegionOwner = 220       // region owner implicit god level should be >= than estate
        }

        ScenePresence m_scenePresence;
        Scene m_scene;
        protected bool m_allowGridGods;
        protected bool m_forceGridGodsOnly;
        protected bool m_regionOwnerIsGod;
        protected bool m_regionManagerIsGod;
        protected bool m_forceGodModeAlwaysOn;
        protected bool m_allowGodActionsWithoutGodMode;

        protected int m_userLevel = 0;

        // the god level from local or grid user rights
        protected int m_rightsGodLevel = 0;

        // the level seen by viewers
        protected int m_viewergodlevel = 0;

        // new level that can be fixed or equal to godlevel, acording to options
        protected int m_godlevel = 0;
        protected int m_lastLevelToViewer = 0;

        public GodController(Scene scene, ScenePresence sp, int userlevel)
        {
            m_scene = scene;
            m_scenePresence = sp;
            m_userLevel = userlevel;

            IConfigSource config = scene.Config;

            string[] sections = new string[] { "Startup", "Permissions" };

            // God level is based on UserLevel. Gods will have that
            // level grid-wide. Others may become god locally but grid
            // gods are god everywhere.
            m_allowGridGods = Util.GetConfigVarFromSections<bool>(config, "allow_grid_gods", sections, false);

            // If grid gods are active, dont allow any other gods
            m_forceGridGodsOnly = Util.GetConfigVarFromSections<bool>(config, "force_grid_gods_only", sections, false);

            if (!m_forceGridGodsOnly)
            {
                // The owner of a region is a god in his region only.
                m_regionOwnerIsGod = Util.GetConfigVarFromSections<bool>(config, "region_owner_is_god", sections, true);

                // Region managers are gods in the regions they manage.
                m_regionManagerIsGod = Util.GetConfigVarFromSections<bool>(config, "region_manager_is_god", sections, false);
            }
            else
            {
                m_allowGridGods = true; // reduce potencial user mistakes
            }

            // God mode should be turned on in the viewer whenever
            // the user has god rights somewhere. They may choose
            // to turn it off again, though.
            m_forceGodModeAlwaysOn = Util.GetConfigVarFromSections<bool>(config, "automatic_gods", sections, false);

            // The user can execute any and all god functions, as
            // permitted by the viewer UI, without actually "godding
            // up". This is the default state in 0.8.2.
            m_allowGodActionsWithoutGodMode = Util.GetConfigVarFromSections<bool>(config, "implicit_gods", sections, false);

            m_rightsGodLevel = CalcRightsGodLevel();

            if (m_allowGodActionsWithoutGodMode)
            {
                m_godlevel = m_rightsGodLevel;
                m_forceGodModeAlwaysOn = false;
            }
            else if (m_forceGodModeAlwaysOn)
            {
                m_viewergodlevel = m_rightsGodLevel;
                m_godlevel = m_rightsGodLevel;
            }

            m_scenePresence.IsGod = (m_godlevel >= 200);
            m_scenePresence.IsViewerUIGod = (m_viewergodlevel >= 200);
        }

        // calculates god level at sp creation from local and grid user god rights
        // for now this is assumed static until user leaves region.
        // later estate and gride level updates may update this
        protected int CalcRightsGodLevel()
        {
            int level = 0;

            if (m_allowGridGods && m_userLevel >= 200)
            {
                level = m_userLevel;
            }

            if (m_forceGridGodsOnly || level >= (int)ImplicitGodLevels.RegionOwner)
            {
                return level;
            }

            if (m_regionOwnerIsGod && m_scene.RegionInfo.EstateSettings.IsEstateOwner(m_scenePresence.UUID))
            {
                level = (int)ImplicitGodLevels.RegionOwner;
            }

            if (level >= (int)ImplicitGodLevels.EstateManager)
            {
                return level;
            }

            if (m_regionManagerIsGod && m_scene.Permissions.IsEstateManager(m_scenePresence.UUID))
            {
                level = (int)ImplicitGodLevels.EstateManager;
            }

            return level;
        }

        protected bool CanBeGod()
        {
            return m_rightsGodLevel >= 200;
        }

        protected void UpdateGodLevels(bool viewerState)
        {
            if (!CanBeGod())
            {
                m_viewergodlevel = 0;
                m_godlevel = 0;
                m_scenePresence.IsGod = false;
                m_scenePresence.IsViewerUIGod = false;
                return;
            }

            // legacy some are controled by viewer, others are static
            if (m_allowGodActionsWithoutGodMode)
            {
                if (viewerState)
                {
                    m_viewergodlevel = m_rightsGodLevel;
                }
                else
                {
                    m_viewergodlevel = 0;
                }

                m_godlevel = m_rightsGodLevel;
            }
            else
            {
                // new all change with viewer
                if (viewerState)
                {
                    m_viewergodlevel = m_rightsGodLevel;
                    m_godlevel = m_rightsGodLevel;
                }
                else
                {
                    m_viewergodlevel = 0;
                    m_godlevel = 0;
                }
            }

            m_scenePresence.IsGod = (m_godlevel >= 200);
            m_scenePresence.IsViewerUIGod = (m_viewergodlevel >= 200);
        }

        public void SyncViewerState()
        {
            if (m_lastLevelToViewer == m_viewergodlevel)
            {
                return;
            }

            m_lastLevelToViewer = m_viewergodlevel;

            if (m_scenePresence.IsChildAgent)
            {
                return;
            }

            m_scenePresence.ControllingClient.SendAdminResponse(UUID.Zero, (uint)m_viewergodlevel);
        }

        public void RequestGodMode(bool god)
        {
            UpdateGodLevels(god);

            if (m_lastLevelToViewer != m_viewergodlevel)
            {
                m_scenePresence.ControllingClient.SendAdminResponse(UUID.Zero, (uint)m_viewergodlevel);
                m_lastLevelToViewer = m_viewergodlevel;
            }
        }

        public OSD State()
        {
            OSDMap godMap = new OSDMap(2);
            bool m_viewerUiIsGod = m_viewergodlevel >= 200;
            godMap.Add("ViewerUiIsGod", OSD.FromBoolean(m_viewerUiIsGod));

            return godMap;
        }

        public void SetState(OSD state)
        {
            bool newstate = false;

            if (m_forceGodModeAlwaysOn)
            {
                newstate = m_viewergodlevel >= 200;
            }

            if (state != null)
            {
                OSDMap s = (OSDMap)state;

                if (s.ContainsKey("ViewerUiIsGod"))
                {
                    newstate = s["ViewerUiIsGod"].AsBoolean();
                }

                m_lastLevelToViewer = m_viewergodlevel; // we are not changing viewer level by default
            }

            UpdateGodLevels(newstate);
        }

        public void HasMovedAway()
        {
            m_lastLevelToViewer = 0;

            if (m_forceGodModeAlwaysOn)
            {
                m_viewergodlevel = m_rightsGodLevel;
                m_godlevel = m_rightsGodLevel;
            }
        }

        public int UserLevel
        {
            get { return m_userLevel; }
            set { m_userLevel = value; }
        }

        public int ViwerUIGodLevel
        {
            get { return m_viewergodlevel; }
        }

        public int GodLevel
        {
            get { return m_godlevel; }
        }
    }
}
