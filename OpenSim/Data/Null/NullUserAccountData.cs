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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using log4net;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;

namespace OpenSim.Data.Null
{
    public class NullUserAccountData : IUserAccountData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<UUID, UserAccountData> m_DataByUUID = new Dictionary<UUID, UserAccountData>();
        private Dictionary<string, UserAccountData> m_DataByName = new Dictionary<string, UserAccountData>();
        private Dictionary<string, UserAccountData> m_DataByEmail = new Dictionary<string, UserAccountData>();

        public NullUserAccountData(string connectionString, string realm)
        {
        }

        /// <summary>
        /// Tries to implement the Get [] semantics, but it cuts corners like crazy.
        /// Specifically, it relies on the knowledge that the only Gets used are
        /// keyed on PrincipalID, Email, and FirstName+LastName.
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public UserAccountData[] Get(string[] fields, string[] values)
        {
            UserAccountData[] userAccounts = new UserAccountData[0];

            List<string> fieldsLst = new List<string>(fields);
            if (fieldsLst.Contains("PrincipalID"))
            {
                int i = fieldsLst.IndexOf("PrincipalID");
                UUID id = UUID.Zero;
                if (UUID.TryParse(values[i], out id))
                    if (m_DataByUUID.ContainsKey(id))
                        userAccounts = new UserAccountData[] { m_DataByUUID[id] };
            }
            else if (fieldsLst.Contains("FirstName") && fieldsLst.Contains("LastName"))
            {
                int findex = fieldsLst.IndexOf("FirstName");
                int lindex = fieldsLst.IndexOf("LastName");
                if (m_DataByName.ContainsKey(values[findex] + " " + values[lindex]))
                {
                    userAccounts = new UserAccountData[] { m_DataByName[values[findex] + " " + values[lindex]] };
                }
            }
            else if (fieldsLst.Contains("Email"))
            {
                int i = fieldsLst.IndexOf("Email");
                if (m_DataByEmail.ContainsKey(values[i]))
                    userAccounts = new UserAccountData[] { m_DataByEmail[values[i]] };
            }

            return userAccounts;
        }

        public bool Store(UserAccountData data)
        {
            if (data == null)
                return false;

            m_log.DebugFormat(
                "[NULL USER ACCOUNT DATA]: Storing user account {0} {1} {2} {3}",
                data.FirstName, data.LastName, data.PrincipalID, this.GetHashCode());

            m_DataByUUID[data.PrincipalID] = data;
            m_DataByName[data.FirstName + " " + data.LastName] = data;
            if (data.Data.ContainsKey("Email") && data.Data["Email"] != null && data.Data["Email"] != string.Empty)
                m_DataByEmail[data.Data["Email"]] = data;

            return true;
        }

        public UserAccountData[] GetUsers(UUID scopeID, string query)
        {
            string[] words = query.Split(new char[] { ' ' });

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length < 3)
                {
                    if (i != words.Length - 1)
                        Array.Copy(words, i + 1, words, i, words.Length - i - 1);
                    Array.Resize(ref words, words.Length - 1);
                }
            }

            if (words.Length == 0)
                return new UserAccountData[0];

            if (words.Length > 2)
                return new UserAccountData[0];

            List<string> lst = new List<string>(m_DataByName.Keys);
            if (words.Length == 1)
            {
                lst = lst.FindAll(delegate(string s) { return s.StartsWith(words[0]); });
            }
            else
            {
                lst = lst.FindAll(delegate(string s) { return s.Contains(words[0]) || s.Contains(words[1]); });
            }

            if (lst == null || (lst != null && lst.Count == 0))
                return new UserAccountData[0];

            UserAccountData[] result = new UserAccountData[lst.Count];
            int n = 0;
            foreach (string key in lst)
                result[n++] = m_DataByName[key];

            return result;
        }

        public bool Delete(string field, string val)
        {
            // Only delete by PrincipalID
            if (field.Equals("PrincipalID"))
            {
                UUID uuid = UUID.Zero;
                if (UUID.TryParse(val, out uuid) && m_DataByUUID.ContainsKey(uuid))
                {
                    UserAccountData account = m_DataByUUID[uuid];
                    m_DataByUUID.Remove(uuid);
                    if (m_DataByName.ContainsKey(account.FirstName + " " + account.LastName))
                        m_DataByName.Remove(account.FirstName + " " + account.LastName);
                    if (account.Data.ContainsKey("Email") && account.Data["Email"] != string.Empty && m_DataByEmail.ContainsKey(account.Data["Email"]))
                        m_DataByEmail.Remove(account.Data["Email"]);

                    return true;
                }
            }

            return false;
        }

        public UserAccountData[] GetUsersWhere(UUID scopeID, string where)
        {
            return null;
        }
    }
}
