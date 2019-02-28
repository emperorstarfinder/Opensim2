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

using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using Nini.Config;
using OpenSim.Framework;

using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.Connectors
{
    public class NeighborServicesConnector : INeighborService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        protected IGridService m_GridService = null;

        public NeighborServicesConnector()
        {
        }

        public NeighborServicesConnector(IGridService gridServices)
        {
            Initialize(gridServices);
        }

        public virtual void Initialize(IGridService gridServices)
        {
            m_GridService = gridServices;
        }

        public virtual GridRegion HelloNeighbor(ulong regionHandle, RegionInfo thisRegion)
        {
            uint x = 0, y = 0;
            Util.RegionHandleToWorldLoc(regionHandle, out x, out y);
            GridRegion regInfo = m_GridService.GetRegionByPosition(thisRegion.ScopeID, (int)x, (int)y);
            if ((regInfo != null) &&
                // Don't remote-call this instance; that's a startup hickup
                !((regInfo.ExternalHostName == thisRegion.ExternalHostName) && (regInfo.HttpPort == thisRegion.HttpPort)))
            {
                if (!DoHelloNeighborCall(regInfo, thisRegion))
                    return null;
            }
            else
                return null;

            return regInfo;
        }

        public bool DoHelloNeighborCall(GridRegion region, RegionInfo thisRegion)
        {
            string uri = region.ServerURI + "region/" + thisRegion.RegionID + "/";
//            m_log.Debug("   >>> DoHelloNeighborCall <<< " + uri);

            WebRequest helloNeighborRequest;

            try
            {
                helloNeighborRequest = WebRequest.Create(uri);
            }
            catch (Exception e)
            {
                m_log.Warn(string.Format(
                    "[Neighbor Services Connector]: Unable to parse uri {0} to send HelloNeighbor from {1} to {2}.  Exception {3} ",
                    uri, thisRegion.RegionName, region.RegionName, e.Message), e);

                return false;
            }

            helloNeighborRequest.Method = "POST";
            helloNeighborRequest.ContentType = "application/json";
            helloNeighborRequest.Timeout = 10000;

            // Fill it in
            OSDMap args = null;
            try
            {
                args = thisRegion.PackRegionInfoData();
            }
            catch (Exception e)
            {
                m_log.Warn(string.Format(
                    "[Neighbor Services Connector]: PackRegionInfoData failed for HelloNeighbor from {0} to {1}.  Exception {2} ",
                    thisRegion.RegionName, region.RegionName, e.Message), e);

                return false;
            }

            // Add the regionhandle of the destination region
            args["destination_handle"] = OSD.FromString(region.RegionHandle.ToString());

            string strBuffer = "";
            byte[] buffer = new byte[1];

            try
            {
                strBuffer = OSDParser.SerializeJsonString(args);
                buffer = Util.UTF8NoBomEncoding.GetBytes(strBuffer);
            }
            catch (Exception e)
            {
                m_log.Warn(string.Format(
                    "[Neighbor Services Connector]: Exception thrown on serialization of HelloNeighbor from {0} to {1}.  Exception {2} ",
                    thisRegion.RegionName, region.RegionName, e.Message), e);

                return false;
            }

            Stream os = null;
            try
            { // send the Post
                helloNeighborRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = helloNeighborRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
            }
            catch
            {
                return false;
            }
            finally
            {
                if (os != null)
                {
                    os.Dispose();
                }
            }

            try
            {
                using (WebResponse webResponse = helloNeighborRequest.GetResponse())
                {
                    if (webResponse == null)
                    {
                        m_log.DebugFormat(
                            "[Neighbor Services Connector]: Null reply on DoHelloNeighborCall post from {0} to {1}",
                            thisRegion.RegionName, region.RegionName);
                    }

                    using (Stream s = webResponse.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(s))
                        {
                            sr.ReadToEnd(); // just try to read
                            //reply = sr.ReadToEnd().Trim();
                            //m_log.InfoFormat("[REST COMMS]: DoHelloNeighborCall reply was {0} ", reply);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Warn(string.Format(
                    "[Neighbor Services Connector]: Exception on reply of DoHelloNeighborCall from {0} back to {1}.  Exception {2} ",
                    region.RegionName, thisRegion.RegionName, e.Message), e);

                return false;
            }

            return true;
        }
    }
}
