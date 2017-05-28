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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Text.RegularExpressions;
using libsecondlife;
using log4net;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Mapping.Attributes;
using NHibernate.Tool.hbm2ddl;
using OpenSim.Framework;
using Environment=NHibernate.Cfg.Environment;

namespace OpenSim.Data.NHibernate
{
    /// <summary>
    /// A User storage interface for the DB4o database system
    /// </summary>
    public class NHibernateAssetData : AssetDataBase, IDisposable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Configuration cfg;
        private ISessionFactory factory;

        public override void Initialise()
        {
            Initialise("SQLiteDialect;SqliteClientDriver;URI=file:Asset.db,version=3");
        }

        public override void Initialise(string connect)
        {
            // Split out the dialect, driver, and connect string
            char[] split = {';'};
            string[] parts = connect.Split(split, 3);
            if (parts.Length != 3)
            {
                // TODO: make this a real exception type
                throw new Exception("Malformed Inventory connection string '" + connect + "'");
            }

            // NHibernate setup
            cfg = new Configuration();
            cfg.SetProperty(Environment.ConnectionProvider,
                            "NHibernate.Connection.DriverConnectionProvider");
            cfg.SetProperty(Environment.Dialect,
                            "NHibernate.Dialect." + parts[0]);
            cfg.SetProperty(Environment.ConnectionDriver,
                            "NHibernate.Driver." + parts[1]);
            cfg.SetProperty(Environment.ConnectionString, parts[2]);
            cfg.AddAssembly("OpenSim.Data.NHibernate");

            HbmSerializer.Default.Validate = true;
            using (MemoryStream stream =
                   HbmSerializer.Default.Serialize(Assembly.GetExecutingAssembly()))
                cfg.AddInputStream(stream);

            factory  = cfg.BuildSessionFactory();

            // If uncommented this will auto create tables, but it
            // does drops of the old tables, so we need a smarter way
            // to acturally manage this.

            // new SchemaExport(cfg).Create(true, true);

            InitDB();
        }

        private void InitDB()
        {
            string regex = @"no such table: Assets";
            Regex RE = new Regex(regex, RegexOptions.Multiline);
            try
            {
                using (ISession session = factory.OpenSession())
                {
                    session.Load(typeof(AssetBase), LLUUID.Zero);
                }
            }
            catch (ObjectNotFoundException)
            {
                // yes, we know it's not there, but that's ok
            }
            catch (ADOException e)
            {
                Match m = RE.Match(e.ToString());
                if (m.Success)
                {
                    // We don't have this table, so create it.
                    new SchemaExport(cfg).Create(true, true);
                }
            }
        }

        override public AssetBase FetchAsset(LLUUID uuid)
        {
            using (ISession session = factory.OpenSession())
            {
                try
                {
                    return session.Load(typeof(AssetBase), uuid) as AssetBase;
                }
                catch
                {
                    return null;
                }
            }
        }

        override public void CreateAsset(AssetBase asset)
        {
            if (!ExistsAsset(asset.FullID))
            {
                using (ISession session = factory.OpenSession())
                {
                    using (ITransaction transaction = session.BeginTransaction())
                    {
                        session.Save(asset);
                        transaction.Commit();
                    }
                }
            }
        }

        override public void UpdateAsset(AssetBase asset)
        {
            if (ExistsAsset(asset.FullID))
            {
                using (ISession session = factory.OpenSession())
                {
                    using (ITransaction transaction = session.BeginTransaction())
                    {
                        session.Update(asset);
                        transaction.Commit();
                    }
                }
            }
        }

        private void LogAssetLoad(AssetBase asset)
        {
            string temporary = asset.Temporary ? "Temporary" : "Stored";
            string local = asset.Local ? "Local" : "Remote";

            int assetLength = (asset.Data != null) ? asset.Data.Length : 0;

            m_log.Info("[SQLITE]: " +
                                     string.Format("Loaded {6} {5} Asset: [{0}][{3}/{4}] \"{1}\":{2} ({7} bytes)",
                                                   asset.FullID, asset.Name, asset.Description, asset.Type,
                                                   asset.InvType, temporary, local, assetLength));
        }

        override public bool ExistsAsset(LLUUID uuid)
        {
            return (FetchAsset(uuid) != null) ? true : false;
        }

        public void DeleteAsset(LLUUID uuid)
        {

        }

        override public void CommitAssets() // force a sync to the database
        {
            m_log.Info("[SQLITE]: Attempting commit");
        }


        public override string Name {
            get { return "NHibernate"; }
        }

        public override string Version {
            get { return "0.1"; }
        }

        public void Dispose()
        {

        }
    }
}