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
using Axiom.Math;
using libsecondlife;

namespace OpenSim.Region.Environment.Scenes.Scripting
{
    public class NullScriptHost : IScriptHost
    {
        private LLVector3 m_pos = new LLVector3(128, 128, 30);

        public string Name
        {
            get { return "Object"; }
            set { }
        }

        public string SitName
        {
            get { return String.Empty; }
            set { }
        }

        public string TouchName
        {
            get { return String.Empty; }
            set { }
        }

        public string Description
        {
            get { return String.Empty; }
            set { }
        }

        public LLUUID UUID
        {
            get { return LLUUID.Zero; }
        }

        public LLUUID ObjectOwner
        {
            get { return LLUUID.Zero; }
        }

        public LLUUID ObjectCreator
        {
            get { return LLUUID.Zero; }
        }

        public LLVector3 AbsolutePosition
        {
            get { return m_pos; }
        }

        public void SetText(string text, Vector3 color, double alpha)
        {
            Console.WriteLine("Tried to SetText [{0}] on NullScriptHost", text);
        }
    }
}