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
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using libsecondlife;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A database interface class to a user profile storage system
    /// </summary>
    public class MSSQLUserData : UserDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Database manager for MySQL
        /// </summary>
        public MSSQLManager database;

        private string m_agentsTableName;
        private string m_usersTableName;
        private string m_userFriendsTableName;

        /// <summary>
        /// Loads and initialises the MySQL storage plugin
        /// </summary>
        override public void Initialise(string connect)
        {
            // TODO: do something with the connect string instead of
            // ignoring it.

            IniFile iniFile = new IniFile("mssql_connection.ini");
            string settingDataSource = iniFile.ParseFileReadValue("data_source");
            string settingInitialCatalog = iniFile.ParseFileReadValue("initial_catalog");
            string settingPersistSecurityInfo = iniFile.ParseFileReadValue("persist_security_info");
            string settingUserId = iniFile.ParseFileReadValue("user_id");
            string settingPassword = iniFile.ParseFileReadValue("password");

            m_usersTableName = iniFile.ParseFileReadValue("userstablename");
            if (m_usersTableName == null)
            {
                m_usersTableName = "users";
            }

            m_userFriendsTableName = iniFile.ParseFileReadValue("userfriendstablename");
            if (m_userFriendsTableName == null)
            {
                m_userFriendsTableName = "userfriends";
            }

            m_agentsTableName = iniFile.ParseFileReadValue("agentstablename");
            if (m_agentsTableName == null)
            {
                m_agentsTableName = "agents";
            }

            database =
                new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId,
                                 settingPassword);

            TestTables();
        }

        private bool TestTables()
        {
            IDbCommand cmd;

            cmd = database.Query("select top 1 * from " + m_usersTableName, new Dictionary<string, string>());
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch
            {
                database.ExecuteResourceSql("Mssql-users.sql");
            }

            cmd = database.Query("select top 1 * from " + m_agentsTableName, new Dictionary<string, string>());
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch
            {
                database.ExecuteResourceSql("Mssql-agents.sql");
            }

            cmd = database.Query("select top 1 * from " + m_userFriendsTableName, new Dictionary<string, string>());
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch
            {
                database.ExecuteResourceSql("CreateUserFriendsTable.sql");
            }

            return true;
        }
        /// <summary>
        /// Searches the database for a specified user profile by name components
        /// </summary>
        /// <param name="user">The first part of the account name</param>
        /// <param name="last">The second part of the account name</param>
        /// <returns>A user profile</returns>
        override public UserProfileData GetUserByName(string user, string last)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["first"] = user;
                    param["second"] = last;

                    IDbCommand result =
                        database.Query("SELECT * FROM " + m_usersTableName + " WHERE username = @first AND lastname = @second", param);
                    IDataReader reader = result.ExecuteReader();

                    UserProfileData row = database.readUserRow(reader);

                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return null;
            }
        }

        #region User Friends List Data

        override public void AddNewUserFriend(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            int dtvalue = Util.UnixTimeSinceEpoch();

            Dictionary<string, string> param = new Dictionary<string, string>();
            param["@ownerID"] = friendlistowner.UUID.ToString();
            param["@friendID"] = friend.UUID.ToString();
            param["@friendPerms"] = perms.ToString();
            param["@datetimestamp"] = dtvalue.ToString();

            try
            {
                lock (database)
                {
                    IDbCommand adder =
                        database.Query(
                        "INSERT INTO " + m_userFriendsTableName + " " +
                        "(ownerID,friendID,friendPerms,datetimestamp) " +
                        "VALUES " +
                        "(@ownerID,@friendID,@friendPerms,@datetimestamp)",
                            param);

                    adder.ExecuteNonQuery();

                    adder =
                        database.Query(
                        "INSERT INTO " + m_userFriendsTableName + " " +
                        "(ownerID,friendID,friendPerms,datetimestamp) " +
                        "VALUES " +
                        "(@friendID,@ownerID,@friendPerms,@datetimestamp)",
                            param);
                    adder.ExecuteNonQuery();

                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return;
            }
        }

        override public void RemoveUserFriend(LLUUID friendlistowner, LLUUID friend)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["@ownerID"] = friendlistowner.UUID.ToString();
            param["@friendID"] = friend.UUID.ToString();


            try
            {
                lock (database)
                {
                    IDbCommand updater =
                        database.Query(
                        "delete from " + m_userFriendsTableName + " where ownerID = @ownerID and friendID = @friendID",
                            param);
                    updater.ExecuteNonQuery();

                    updater =
                        database.Query(
                        "delete from " + m_userFriendsTableName + " where ownerID = @friendID and friendID = @ownerID",
                            param);
                    updater.ExecuteNonQuery();

                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return;
            }
        }

        override public void UpdateUserFriendPerms(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["@ownerID"] = friendlistowner.UUID.ToString();
            param["@friendID"] = friend.UUID.ToString();
            param["@friendPerms"] = perms.ToString();


            try
            {
                lock (database)
                {
                    IDbCommand updater =
                        database.Query(
                        "update " + m_userFriendsTableName +
                        " SET friendPerms = @friendPerms " +
                        "where ownerID = @ownerID and friendID = @friendID",
                            param);

                    updater.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return;
            }
        }


        override public List<FriendListItem> GetUserFriendList(LLUUID friendlistowner)
        {
            List<FriendListItem> Lfli = new List<FriendListItem>();

            Dictionary<string, string> param = new Dictionary<string, string>();
            param["@ownerID"] = friendlistowner.UUID.ToString();

            try
            {
                lock (database)
                {
                    //Left Join userfriends to itself
                    IDbCommand result =
                        database.Query(
                        "select a.ownerID,a.friendID,a.friendPerms,b.friendPerms as ownerperms from " + m_userFriendsTableName + " as a, " + m_userFriendsTableName + " as b" +
                        " where a.ownerID = @ownerID and b.ownerID = a.friendID and b.friendID = a.ownerID",
                            param);
                    IDataReader reader = result.ExecuteReader();


                    while (reader.Read())
                    {
                        FriendListItem fli = new FriendListItem();
                        fli.FriendListOwner = new LLUUID((string)reader["ownerID"]);
                        fli.Friend = new LLUUID((string)reader["friendID"]);
                        fli.FriendPerms = (uint)Convert.ToInt32(reader["friendPerms"]);

                        // This is not a real column in the database table, it's a joined column from the opposite record
                        fli.FriendListOwnerPerms = (uint)Convert.ToInt32(reader["ownerperms"]);

                        Lfli.Add(fli);
                    }
                    reader.Close();
                    result.Dispose();
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return Lfli;
            }

            return Lfli;
        }

        #endregion

        override public void UpdateUserCurrentRegion(LLUUID avatarid, LLUUID regionuuid, ulong regionhandle)
        {
            //m_log.Info("[USER]: Stub UpdateUserCUrrentRegion called");
        }



        override public List<AvatarPickerAvatar> GeneratePickerResults(LLUUID queryID, string query)
        {
            List<AvatarPickerAvatar> returnlist = new List<AvatarPickerAvatar>();
            string[] querysplit;
            querysplit = query.Split(' ');
            if (querysplit.Length == 2)
            {
                try
                {
                    lock (database)
                    {
                        Dictionary<string, string> param = new Dictionary<string, string>();
                        param["first"] = querysplit[0];
                        param["second"] = querysplit[1];

                        IDbCommand result =
                            database.Query(
                                "SELECT UUID,username,lastname FROM " + m_usersTableName + " WHERE username = @first AND lastname = @second",
                                param);
                        IDataReader reader = result.ExecuteReader();


                        while (reader.Read())
                        {
                            AvatarPickerAvatar user = new AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string)reader["UUID"]);
                            user.firstName = (string)reader["username"];
                            user.lastName = (string)reader["lastname"];
                            returnlist.Add(user);
                        }
                        reader.Close();
                        result.Dispose();
                    }
                }
                catch (Exception e)
                {
                    database.Reconnect();
                    m_log.Error(e.ToString());
                    return returnlist;
                }
            }
            else if (querysplit.Length == 1)
            {
                try
                {
                    lock (database)
                    {
                        Dictionary<string, string> param = new Dictionary<string, string>();
                        param["first"] = querysplit[0];

                        IDbCommand result =
                            database.Query(
                                "SELECT UUID,username,lastname FROM " + m_usersTableName + " WHERE username = @first OR lastname = @first",
                                param);
                        IDataReader reader = result.ExecuteReader();


                        while (reader.Read())
                        {
                            AvatarPickerAvatar user = new AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string)reader["UUID"]);
                            user.firstName = (string)reader["username"];
                            user.lastName = (string)reader["lastname"];
                            returnlist.Add(user);
                        }
                        reader.Close();
                        result.Dispose();
                    }
                }
                catch (Exception e)
                {
                    database.Reconnect();
                    m_log.Error(e.ToString());
                    return returnlist;
                }
            }
            return returnlist;
        }

        // See IUserData
        override public UserProfileData GetUserByUUID(LLUUID uuid)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["uuid"] = uuid.ToString();

                    IDbCommand result = database.Query("SELECT * FROM " + m_usersTableName + " WHERE UUID = @uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    UserProfileData row = database.readUserRow(reader);

                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a user session searching by name
        /// </summary>
        /// <param name="name">The account name</param>
        /// <returns>The users session</returns>
        override public UserAgentData GetAgentByName(string name)
        {
            return GetAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a user session by account name
        /// </summary>
        /// <param name="user">First part of the users account name</param>
        /// <param name="last">Second part of the users account name</param>
        /// <returns>The users session</returns>
        override public UserAgentData GetAgentByName(string user, string last)
        {
            UserProfileData profile = GetUserByName(user, last);
            return GetAgentByUUID(profile.ID);
        }

        /// <summary>
        /// Returns an agent session by account UUID
        /// </summary>
        /// <param name="uuid">The accounts UUID</param>
        /// <returns>The users session</returns>
        override public UserAgentData GetAgentByUUID(LLUUID uuid)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["uuid"] = uuid.ToString();

                    IDbCommand result = database.Query("SELECT * FROM " + m_agentsTableName + " WHERE UUID = @uuid", param);
                    IDataReader reader = result.ExecuteReader();

                    UserAgentData row = database.readAgentRow(reader);

                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
                return null;
            }
        }
        override public void StoreWebLoginKey(LLUUID AgentID, LLUUID WebLoginKey)
        {
            UserProfileData user = GetUserByUUID(AgentID);
            user.WebLoginKey = WebLoginKey;
            UpdateUserProfile(user);

        }
        /// <summary>
        /// Creates a new users profile
        /// </summary>
        /// <param name="user">The user profile to create</param>
        override public void AddNewUserProfile(UserProfileData user)
        {
            try
            {
                lock (database)
                {
                    InsertUserRow(user.ID, user.FirstName, user.SurName, user.PasswordHash, user.PasswordSalt,
                                           user.HomeRegion, user.HomeLocation.X, user.HomeLocation.Y,
                                           user.HomeLocation.Z,
                                           user.HomeLookAt.X, user.HomeLookAt.Y, user.HomeLookAt.Z, user.Created,
                                           user.LastLogin, user.UserInventoryURI, user.UserAssetURI,
                                           user.CanDoMask, user.WantDoMask,
                                           user.AboutText, user.FirstLifeAboutText, user.Image,
                                           user.FirstLifeImage, user.WebLoginKey);
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Creates a new user and inserts it into the database
        /// </summary>
        /// <param name="uuid">User ID</param>
        /// <param name="username">First part of the login</param>
        /// <param name="lastname">Second part of the login</param>
        /// <param name="passwordHash">A salted hash of the users password</param>
        /// <param name="passwordSalt">The salt used for the password hash</param>
        /// <param name="homeRegion">A regionHandle of the users home region</param>
        /// <param name="homeLocX">Home region position vector</param>
        /// <param name="homeLocY">Home region position vector</param>
        /// <param name="homeLocZ">Home region position vector</param>
        /// <param name="homeLookAtX">Home region 'look at' vector</param>
        /// <param name="homeLookAtY">Home region 'look at' vector</param>
        /// <param name="homeLookAtZ">Home region 'look at' vector</param>
        /// <param name="created">Account created (unix timestamp)</param>
        /// <param name="lastlogin">Last login (unix timestamp)</param>
        /// <param name="inventoryURI">Users inventory URI</param>
        /// <param name="assetURI">Users asset URI</param>
        /// <param name="canDoMask">I can do mask</param>
        /// <param name="wantDoMask">I want to do mask</param>
        /// <param name="aboutText">Profile text</param>
        /// <param name="firstText">Firstlife text</param>
        /// <param name="profileImage">UUID for profile image</param>
        /// <param name="firstImage">UUID for firstlife image</param>
        /// <returns>Success?</returns>
        private bool InsertUserRow(LLUUID uuid, string username, string lastname, string passwordHash,
                                  string passwordSalt, UInt64 homeRegion, float homeLocX, float homeLocY, float homeLocZ,
                                  float homeLookAtX, float homeLookAtY, float homeLookAtZ, int created, int lastlogin,
                                  string inventoryURI, string assetURI, uint canDoMask, uint wantDoMask,
                                  string aboutText, string firstText,
                                  LLUUID profileImage, LLUUID firstImage, LLUUID webLoginKey)
        {
            string sql = "INSERT INTO "+m_usersTableName;
            sql += " ([UUID], [username], [lastname], [passwordHash], [passwordSalt], [homeRegion], ";
            sql +=
                "[homeLocationX], [homeLocationY], [homeLocationZ], [homeLookAtX], [homeLookAtY], [homeLookAtZ], [created], ";
            sql +=
                "[lastLogin], [userInventoryURI], [userAssetURI], [profileCanDoMask], [profileWantDoMask], [profileAboutText], ";
            sql += "[profileFirstText], [profileImage], [profileFirstImage], [webLoginKey]) VALUES ";

            sql += "(@UUID, @username, @lastname, @passwordHash, @passwordSalt, @homeRegion, ";
            sql +=
                "@homeLocationX, @homeLocationY, @homeLocationZ, @homeLookAtX, @homeLookAtY, @homeLookAtZ, @created, ";
            sql +=
                "@lastLogin, @userInventoryURI, @userAssetURI, @profileCanDoMask, @profileWantDoMask, @profileAboutText, ";
            sql += "@profileFirstText, @profileImage, @profileFirstImage, @webLoginKey);";

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters["UUID"] = uuid.ToString();
            parameters["username"] = username.ToString();
            parameters["lastname"] = lastname.ToString();
            parameters["passwordHash"] = passwordHash.ToString();
            parameters["passwordSalt"] = passwordSalt.ToString();
            parameters["homeRegion"] = homeRegion.ToString();
            parameters["homeLocationX"] = homeLocX.ToString();
            parameters["homeLocationY"] = homeLocY.ToString();
            parameters["homeLocationZ"] = homeLocZ.ToString();
            parameters["homeLookAtX"] = homeLookAtX.ToString();
            parameters["homeLookAtY"] = homeLookAtY.ToString();
            parameters["homeLookAtZ"] = homeLookAtZ.ToString();
            parameters["created"] = created.ToString();
            parameters["lastLogin"] = lastlogin.ToString();
            parameters["userInventoryURI"] = String.Empty;
            parameters["userAssetURI"] = String.Empty;
            parameters["profileCanDoMask"] = "0";
            parameters["profileWantDoMask"] = "0";
            parameters["profileAboutText"] = aboutText;
            parameters["profileFirstText"] = firstText;
            parameters["profileImage"] = profileImage.ToString();
            parameters["profileFirstImage"] = firstImage.ToString();
            parameters["webLoginKey"] = LLUUID.Random().ToString();

            bool returnval = false;

            try
            {
                IDbCommand result = database.Query(sql, parameters);

                if (result.ExecuteNonQuery() == 1)
                    returnval = true;

                result.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                return false;
            }

            return returnval;
        }

        /// <summary>
        /// Creates a new agent
        /// </summary>
        /// <param name="agent">The agent to create</param>
        override public void AddNewUserAgent(UserAgentData agent)
        {
            // Do nothing.
        }


        override public bool UpdateUserProfile(UserProfileData user)
        {
            SqlCommand command = new SqlCommand("UPDATE " + m_usersTableName + " set UUID = @uuid, " +
                                                "username = @username, " +
                                                "lastname = @lastname," +
                                                "passwordHash = @passwordHash," +
                                                "passwordSalt = @passwordSalt," +
                                                "homeRegion = @homeRegion," +
                                                "homeLocationX = @homeLocationX," +
                                                "homeLocationY = @homeLocationY," +
                                                "homeLocationZ = @homeLocationZ," +
                                                "homeLookAtX = @homeLookAtX," +
                                                "homeLookAtY = @homeLookAtY," +
                                                "homeLookAtZ = @homeLookAtZ," +
                                                "created = @created," +
                                                "lastLogin = @lastLogin," +
                                                "userInventoryURI = @userInventoryURI," +
                                                "userAssetURI = @userAssetURI," +
                                                "profileCanDoMask = @profileCanDoMask," +
                                                "profileWantDoMask = @profileWantDoMask," +
                                                "profileAboutText = @profileAboutText," +
                                                "profileFirstText = @profileFirstText," +
                                                "profileImage = @profileImage," +
                                                "profileFirstImage = @profileFirstImage, " +
                                                "webLoginKey = @webLoginKey  where " +
                                                "UUID = @keyUUUID;", database.getConnection());
            SqlParameter param1 = new SqlParameter("@uuid", user.ID.ToString());
            SqlParameter param2 = new SqlParameter("@username", user.FirstName);
            SqlParameter param3 = new SqlParameter("@lastname", user.SurName);
            SqlParameter param4 = new SqlParameter("@passwordHash", user.PasswordHash);
            SqlParameter param5 = new SqlParameter("@passwordSalt", user.PasswordSalt);
            SqlParameter param6 = new SqlParameter("@homeRegion", Convert.ToInt64(user.HomeRegion));
            SqlParameter param7 = new SqlParameter("@homeLocationX", user.HomeLocation.X);
            SqlParameter param8 = new SqlParameter("@homeLocationY", user.HomeLocation.Y);
            SqlParameter param9 = new SqlParameter("@homeLocationZ", user.HomeLocation.Y);
            SqlParameter param10 = new SqlParameter("@homeLookAtX", user.HomeLookAt.X);
            SqlParameter param11 = new SqlParameter("@homeLookAtY", user.HomeLookAt.Y);
            SqlParameter param12 = new SqlParameter("@homeLookAtZ", user.HomeLookAt.Z);
            SqlParameter param13 = new SqlParameter("@created", Convert.ToInt32(user.Created));
            SqlParameter param14 = new SqlParameter("@lastLogin", Convert.ToInt32(user.LastLogin));
            SqlParameter param15 = new SqlParameter("@userInventoryURI", user.UserInventoryURI);
            SqlParameter param16 = new SqlParameter("@userAssetURI", user.UserAssetURI);
            SqlParameter param17 = new SqlParameter("@profileCanDoMask", Convert.ToInt32(user.CanDoMask));
            SqlParameter param18 = new SqlParameter("@profileWantDoMask", Convert.ToInt32(user.WantDoMask));
            SqlParameter param19 = new SqlParameter("@profileAboutText", user.AboutText);
            SqlParameter param20 = new SqlParameter("@profileFirstText", user.FirstLifeAboutText);
            SqlParameter param21 = new SqlParameter("@profileImage", user.Image.ToString());
            SqlParameter param22 = new SqlParameter("@profileFirstImage", user.FirstLifeImage.ToString());
            SqlParameter param23 = new SqlParameter("@keyUUUID", user.ID.ToString());
            SqlParameter param24 = new SqlParameter("@webLoginKey", user.WebLoginKey.UUID.ToString());
            command.Parameters.Add(param1);
            command.Parameters.Add(param2);
            command.Parameters.Add(param3);
            command.Parameters.Add(param4);
            command.Parameters.Add(param5);
            command.Parameters.Add(param6);
            command.Parameters.Add(param7);
            command.Parameters.Add(param8);
            command.Parameters.Add(param9);
            command.Parameters.Add(param10);
            command.Parameters.Add(param11);
            command.Parameters.Add(param12);
            command.Parameters.Add(param13);
            command.Parameters.Add(param14);
            command.Parameters.Add(param15);
            command.Parameters.Add(param16);
            command.Parameters.Add(param17);
            command.Parameters.Add(param18);
            command.Parameters.Add(param19);
            command.Parameters.Add(param20);
            command.Parameters.Add(param21);
            command.Parameters.Add(param22);
            command.Parameters.Add(param23);
            command.Parameters.Add(param24);
            try
            {
                int affected = command.ExecuteNonQuery();
                if (affected != 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
            return false;
        }

        /// <summary>
        /// Performs a money transfer request between two accounts
        /// </summary>
        /// <param name="from">The senders account ID</param>
        /// <param name="to">The receivers account ID</param>
        /// <param name="amount">The amount to transfer</param>
        /// <returns>Success?</returns>
        override public bool MoneyTransferRequest(LLUUID from, LLUUID to, uint amount)
        {
            return false;
        }

        /// <summary>
        /// Performs an inventory transfer request between two accounts
        /// </summary>
        /// <remarks>TODO: Move to inventory server</remarks>
        /// <param name="from">The senders account ID</param>
        /// <param name="to">The receivers account ID</param>
        /// <param name="item">The item to transfer</param>
        /// <returns>Success?</returns>
        override public bool InventoryTransferRequest(LLUUID from, LLUUID to, LLUUID item)
        {
            return false;
        }

        /// Appearance
        /// TODO: stubs for now to get us to a compiling state gently
        override public AvatarAppearance GetUserAppearance(LLUUID user)
        {
//            return new AvatarAppearance();
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["@UUID"] = user.ToString();

                    IDbCommand result =
                        database.Query("SELECT * FROM avatarappearance WHERE owner = @UUID", param);
                    IDataReader reader = result.ExecuteReader();

                    AvatarAppearance item = null;
                    if (reader.Read())
                        item = readUserAppearance(reader);

                    reader.Close();
                    result.Dispose();

                    return item;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                m_log.Error(e.ToString());
            }
            return null;
        }

        /// <summary>
        /// Reads a one item from an SQL result
        /// </summary>
        /// <param name="reader">The SQL Result</param>
        /// <returns>the item read</returns>
        private static AvatarAppearance readUserAppearance(IDataReader reader)
        {
            try
            {
                AvatarAppearance appearance = new AvatarAppearance();

                appearance.Owner = new LLUUID((string)reader["owner"]);
                appearance.Serial = Convert.ToInt32(reader["serial"]);
                appearance.VisualParams = (byte[])reader["visual_params"];
                appearance.Texture = new LLObject.TextureEntry((byte[])reader["texture"], 0, ((byte[])reader["texture"]).Length);
                appearance.AvatarHeight = (float)Convert.ToDouble(reader["avatar_height"]);
                appearance.BodyItem = new LLUUID((string)reader["body_item"]);
                appearance.BodyAsset = new LLUUID((string)reader["body_asset"]);
                appearance.SkinItem = new LLUUID((string)reader["skin_item"]);
                appearance.SkinAsset = new LLUUID((string)reader["skin_asset"]);
                appearance.HairItem = new LLUUID((string)reader["hair_item"]);
                appearance.HairAsset = new LLUUID((string)reader["hair_asset"]);
                appearance.EyesItem = new LLUUID((string)reader["eyes_item"]);
                appearance.EyesAsset = new LLUUID((string)reader["eyes_asset"]);
                appearance.ShirtItem = new LLUUID((string)reader["shirt_item"]);
                appearance.ShirtAsset = new LLUUID((string)reader["shirt_asset"]);
                appearance.PantsItem = new LLUUID((string)reader["pants_item"]);
                appearance.PantsAsset = new LLUUID((string)reader["pants_asset"]);
                appearance.ShoesItem = new LLUUID((string)reader["shoes_item"]);
                appearance.ShoesAsset = new LLUUID((string)reader["shoes_asset"]);
                appearance.SocksItem = new LLUUID((string)reader["socks_item"]);
                appearance.SocksAsset = new LLUUID((string)reader["socks_asset"]);
                appearance.JacketItem = new LLUUID((string)reader["jacket_item"]);
                appearance.JacketAsset = new LLUUID((string)reader["jacket_asset"]);
                appearance.GlovesItem = new LLUUID((string)reader["gloves_item"]);
                appearance.GlovesAsset = new LLUUID((string)reader["gloves_asset"]);
                appearance.UnderShirtItem = new LLUUID((string)reader["undershirt_item"]);
                appearance.UnderShirtAsset = new LLUUID((string)reader["undershirt_asset"]);
                appearance.UnderPantsItem = new LLUUID((string)reader["underpants_item"]);
                appearance.UnderPantsAsset = new LLUUID((string)reader["underpants_asset"]);
                appearance.SkirtItem = new LLUUID((string)reader["skirt_item"]);
                appearance.SkirtAsset = new LLUUID((string)reader["skirt_asset"]);

                return appearance;
            }
            catch (SqlException e)
            {
                m_log.Error(e.ToString());
            }

            return null;
        }

        override public void UpdateUserAppearance(LLUUID user, AvatarAppearance appearance)
        {
            string sql = String.Empty;
            sql += "DELETE FROM avatarappearance WHERE owner=@owner ";
            sql += "INSERT INTO avatarappearance ";
            sql += "(owner, serial, visual_params, texture, avatar_height, ";
            sql += "body_item, body_asset, skin_item, skin_asset, hair_item, hair_asset, eyes_item, eyes_asset, ";
            sql += "shirt_item, shirt_asset, pants_item, pants_asset, shoes_item, shoes_asset, socks_item, socks_asset, ";
            sql += "jacket_item, jacket_asset, gloves_item, gloves_asset, undershirt_item, undershirt_asset, underpants_item, underpants_asset, ";
            sql += "skirt_item, skirt_asset) values (";
            sql += "@owner, @serial, @visual_params, @texture, @avatar_height, ";
            sql += "@body_item, @body_asset, @skin_item, @skin_asset, @hair_item, @hair_asset, @eyes_item, @eyes_asset, ";
            sql += "@shirt_item, @shirt_asset, @pants_item, @pants_asset, @shoes_item, @shoes_asset, @socks_item, @socks_asset, ";
            sql += "@jacket_item, @jacket_asset, @gloves_item, @gloves_asset, @undershirt_item, @undershirt_asset, @underpants_item, @underpants_asset, ";
            sql += "@skirt_item, @skirt_asset)";

            SqlCommand cmd = new SqlCommand(sql, database.getConnection());
            cmd.Parameters.AddWithValue("@owner", appearance.Owner.ToString());
            cmd.Parameters.AddWithValue("@serial", appearance.Serial);
            cmd.Parameters.AddWithValue("@visual_params", appearance.VisualParams);
            cmd.Parameters.AddWithValue("@texture", appearance.Texture.ToBytes());
            cmd.Parameters.AddWithValue("@avatar_height", appearance.AvatarHeight);
            cmd.Parameters.AddWithValue("@body_item", appearance.BodyItem.ToString());
            cmd.Parameters.AddWithValue("@body_asset", appearance.BodyAsset.ToString());
            cmd.Parameters.AddWithValue("@skin_item", appearance.SkinItem.ToString());
            cmd.Parameters.AddWithValue("@skin_asset", appearance.SkinAsset.ToString());
            cmd.Parameters.AddWithValue("@hair_item", appearance.HairItem.ToString());
            cmd.Parameters.AddWithValue("@hair_asset", appearance.HairAsset.ToString());
            cmd.Parameters.AddWithValue("@eyes_item", appearance.EyesItem.ToString());
            cmd.Parameters.AddWithValue("@eyes_asset", appearance.EyesAsset.ToString());
            cmd.Parameters.AddWithValue("@shirt_item", appearance.ShirtItem.ToString());
            cmd.Parameters.AddWithValue("@shirt_asset", appearance.ShirtAsset.ToString());
            cmd.Parameters.AddWithValue("@pants_item", appearance.PantsItem.ToString());
            cmd.Parameters.AddWithValue("@pants_asset", appearance.PantsAsset.ToString());
            cmd.Parameters.AddWithValue("@shoes_item", appearance.ShoesItem.ToString());
            cmd.Parameters.AddWithValue("@shoes_asset", appearance.ShoesAsset.ToString());
            cmd.Parameters.AddWithValue("@socks_item", appearance.SocksItem.ToString());
            cmd.Parameters.AddWithValue("@socks_asset", appearance.SocksAsset.ToString());
            cmd.Parameters.AddWithValue("@jacket_item", appearance.JacketItem.ToString());
            cmd.Parameters.AddWithValue("@jacket_asset", appearance.JacketAsset.ToString());
            cmd.Parameters.AddWithValue("@gloves_item", appearance.GlovesItem.ToString());
            cmd.Parameters.AddWithValue("@gloves_asset", appearance.GlovesAsset.ToString());
            cmd.Parameters.AddWithValue("@undershirt_item", appearance.UnderShirtItem.ToString());
            cmd.Parameters.AddWithValue("@undershirt_asset", appearance.UnderShirtAsset.ToString());
            cmd.Parameters.AddWithValue("@underpants_item", appearance.UnderPantsItem.ToString());
            cmd.Parameters.AddWithValue("@underpants_asset", appearance.UnderPantsAsset.ToString());
            cmd.Parameters.AddWithValue("@skirt_item", appearance.SkirtItem.ToString());
            cmd.Parameters.AddWithValue("@skirt_asset", appearance.SkirtAsset.ToString());
                
            try
            {
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
            return;
        }

        override public void AddAttachment(LLUUID user, LLUUID item)
        {
            return;
        }

        override public void RemoveAttachment(LLUUID user, LLUUID item)
        {
            return;
        }

        override public List<LLUUID> GetAttachments(LLUUID user)
        {
            return new List<LLUUID>();
        }

        /// <summary>
        /// Database provider name
        /// </summary>
        /// <returns>Provider name</returns>
        override public string Name
        {
            get {return "MSSQL Userdata Interface";}
        }

        /// <summary>
        /// Database provider version
        /// </summary>
        /// <returns>provider version</returns>
        override public string Version
        {
            get {return database.getVersion();}
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        /// <param name="query"></param>
        public void runQuery(string query)
        {
        }
    }
}