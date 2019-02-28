/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;

using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nini.Config;
using log4net;


namespace OpenSim.Server.Handlers.Neighbor
{
    public class NeighborGetHandler : BaseStreamHandler
    {
        // TODO: unused: private ISimulationService m_SimulationService;
        // TODO: unused: private IAuthenticationService m_AuthenticationService;

        public NeighborGetHandler(INeighborService service, IAuthenticationService authentication) :
                base("GET", "/region")
        {
            // TODO: unused: m_SimulationService = service;
            // TODO: unused: m_AuthenticationService = authentication;
        }

        protected override byte[] ProcessRequest(string path, Stream request,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            // Not implemented yet
            Console.WriteLine("--- Get region --- " + path);
            httpResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
            return new byte[] { };
        }
    }

    public class NeighborPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private INeighborService m_NeighborService;
        private IAuthenticationService m_AuthenticationService;
        // TODO: unused: private bool m_AllowForeignGuests;

        public NeighborPostHandler(INeighborService service, IAuthenticationService authentication) :
            base("POST", "/region")
        {
            m_NeighborService = service;
            m_AuthenticationService = authentication;
            // TODO: unused: m_AllowForeignGuests = foreignGuests;
        }

        protected override byte[] ProcessRequest(string path, Stream request,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            byte[] result = new byte[0];

            UUID regionID;
            string action;
            ulong regionHandle;
            if (RestHandlerUtils.GetParams(path, out regionID, out regionHandle, out action))
            {
                m_log.InfoFormat("[RegionPostHandler]: Invalid parameters for neighbor message {0}", path);
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                httpResponse.StatusDescription = "Invalid parameters for neighbor message " + path;

                return result;
            }

            if (m_AuthenticationService != null)
            {
                // Authentication
                string authority = string.Empty;
                string authToken = string.Empty;
                if (!RestHandlerUtils.GetAuthentication(httpRequest, out authority, out authToken))
                {
                    m_log.InfoFormat("[RegionPostHandler]: Authentication failed for neighbor message {0}", path);
                    httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return result;
                }
                // TODO: Rethink this
                //if (!m_AuthenticationService.VerifyKey(regionID, authToken))
                //{
                //    m_log.InfoFormat("[RegionPostHandler]: Authentication failed for neighbor message {0}", path);
                //    httpResponse.StatusCode = (int)HttpStatusCode.Forbidden;
                //    return result;
                //}
                m_log.DebugFormat("[RegionPostHandler]: Authentication succeeded for {0}", regionID);
            }

            OSDMap args = Util.GetOSDMap(request, (int)httpRequest.ContentLength);
            if (args == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                httpResponse.StatusDescription = "Unable to retrieve data";
                m_log.DebugFormat("[RegionPostHandler]: Unable to retrieve data for post {0}", path);
                return result;
            }

            // retrieve the regionhandle
            ulong regionhandle = 0;
            if (args["destination_handle"] != null)
                UInt64.TryParse(args["destination_handle"].AsString(), out regionhandle);

            RegionInfo aRegion = new RegionInfo();
            try
            {
                aRegion.UnpackRegionInfoData(args);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[RegionPostHandler]: exception on unpacking region info {0}", ex.Message);
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                httpResponse.StatusDescription = "Problems with data deserialization";
                return result;
            }

            // Finally!
            GridRegion thisRegion = m_NeighborService.HelloNeighbor(regionhandle, aRegion);

            OSDMap resp = new OSDMap(1);

            if (thisRegion != null)
                resp["success"] = OSD.FromBoolean(true);
            else
                resp["success"] = OSD.FromBoolean(false);

            httpResponse.StatusCode = (int)HttpStatusCode.OK;

            return Util.UTF8.GetBytes(OSDParser.SerializeJsonString(resp));
        }
    }

    public class NeighborPutHandler : BaseStreamHandler
    {
        // TODO: unused: private ISimulationService m_SimulationService;
        // TODO: unused: private IAuthenticationService m_AuthenticationService;

        public NeighborPutHandler(INeighborService service, IAuthenticationService authentication) :
            base("PUT", "/region")
        {
            // TODO: unused: m_SimulationService = service;
            // TODO: unused: m_AuthenticationService = authentication;
        }

        protected override byte[] ProcessRequest(string path, Stream request,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            // Not implemented yet
            httpResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
            return new byte[] { };
        }
    }

    public class NeighborDeleteHandler : BaseStreamHandler
    {
        // TODO: unused: private ISimulationService m_SimulationService;
        // TODO: unused: private IAuthenticationService m_AuthenticationService;

        public NeighborDeleteHandler(INeighborService service, IAuthenticationService authentication) :
            base("DELETE", "/region")
        {
            // TODO: unused: m_SimulationService = service;
            // TODO: unused: m_AuthenticationService = authentication;
        }

        protected override byte[] ProcessRequest(string path, Stream request,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            // Not implemented yet
            httpResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
            return new byte[] { };
        }
    }
}
