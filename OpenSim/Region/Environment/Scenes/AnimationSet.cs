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
using libsecondlife;

namespace OpenSim.Region.Environment.Scenes
{
    public class AnimationSet
    {
        public static AvatarAnimations Animations = new AvatarAnimations();

        private Animation m_defaultAnimation = new Animation();
        private List<Animation> m_animations = new List<Animation>();

        public AnimationSet()
        {
            ResetDefaultAnimation();
        }

        public bool HasAnimation(LLUUID animID)
        {
            if (m_defaultAnimation.AnimID == animID)
                return true;

            for (int i = 0; i < m_animations.Count; ++i)
            {
                if (m_animations[i].AnimID == animID)
                    return true;
            }

            return false;
        }

        public bool Add(LLUUID animID, int sequenceNum)
        {
            lock (m_animations)
            {
                if (!HasAnimation(animID))
                {
                    m_animations.Add(new Animation(animID, sequenceNum));
                    return true;
                }
            }
            return false;
        }

        public bool Remove(LLUUID animID)
        {
            lock (m_animations)
            {
                if (m_defaultAnimation.AnimID == animID)
                {
                    ResetDefaultAnimation();
                }
                else if (HasAnimation(animID))
                {
                    for (int i = 0; i < m_animations.Count; i++)
                    {
                        if (m_animations[i].AnimID == animID)
                        {
                            m_animations.RemoveAt(i);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void Clear()
        {
            ResetDefaultAnimation();
            m_animations.Clear();
        }

        /// <summary>
        /// The default animation is reserved for "main" animations
        /// that are mutually exclusive, e.g. flying and sitting.
        /// </summary>
        public bool SetDefaultAnimation(LLUUID animID, int sequenceNum)
        {
            if (m_defaultAnimation.AnimID != animID)
            {
                m_defaultAnimation = new Animation(animID, sequenceNum);
                return true;
            }
            return false;
        }

        protected bool ResetDefaultAnimation()
        {
            return TrySetDefaultAnimation("STAND", 1);
        }

        /// <summary>
        /// Set the animation as the default animation if it's known
        /// </summary>
        public bool TrySetDefaultAnimation(string anim, int sequenceNum)
        {
            if (Animations.AnimsLLUUID.ContainsKey(anim))
            {
                return SetDefaultAnimation(Animations.AnimsLLUUID[anim], sequenceNum);
            }
            return false;
        }

        public void GetArrays(out LLUUID[] animIDs, out int[] sequenceNums)
        {
            lock (m_animations)
            {
                animIDs = new LLUUID[m_animations.Count + 1];
                sequenceNums = new int[m_animations.Count + 1];

                animIDs[0] = m_defaultAnimation.AnimID;
                sequenceNums[0] = m_defaultAnimation.SequenceNum;

                for (int i = 0; i < m_animations.Count; ++i)
                {
                    animIDs[i + 1] = m_animations[i].AnimID;
                    sequenceNums[i + 1] = m_animations[i].SequenceNum;
                }
            }
        }
    }
}