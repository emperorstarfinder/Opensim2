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

/* Original code: Tedd Hansen */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Grid.ScriptEngine.DotNetEngine.Compiler.LSL;
using OpenSim.Region.Environment.Scenes.Scripting;

namespace OpenSim.Grid.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// EventQueueManager handles event queues
    /// Events are queued and executed in separate thread
    /// </summary>
    [Serializable]
    internal class EventQueueManager
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// List of threads processing event queue
        /// </summary>
        private List<Thread> eventQueueThreads = new List<Thread>();

        private object queueLock = new object(); // Mutex lock object

        /// <summary>
        /// How many ms to sleep if queue is empty
        /// </summary>
        private int nothingToDoSleepms = 50;

        /// <summary>
        /// How many threads to process queue with
        /// </summary>
        private int numberOfThreads = 2;

        /// <summary>
        /// Queue containing events waiting to be executed
        /// </summary>
        private Queue<QueueItemStruct> eventQueue = new Queue<QueueItemStruct>();

        /// <summary>
        /// Queue item structure
        /// </summary>
        private struct QueueItemStruct
        {
            public uint localID;
            public LLUUID itemID;
            public string functionName;
            public object[] param;
        }

        /// <summary>
        /// List of localID locks for mutex processing of script events
        /// </summary>
        private List<uint> objectLocks = new List<uint>();

        private object tryLockLock = new object(); // Mutex lock object

        private ScriptEngine m_ScriptEngine;

        public EventQueueManager(ScriptEngine _ScriptEngine)
        {
            m_ScriptEngine = _ScriptEngine;

            //
            // Start event queue processing threads (worker threads)
            //
            for (int ThreadCount = 0; ThreadCount <= numberOfThreads; ThreadCount++)
            {
                Thread EventQueueThread = new Thread(EventQueueThreadLoop);
                eventQueueThreads.Add(EventQueueThread);
                EventQueueThread.IsBackground = true;
                EventQueueThread.Priority = ThreadPriority.BelowNormal;
                EventQueueThread.Name = "EventQueueManagerThread_" + ThreadCount;
                EventQueueThread.Start();
            }
        }

        ~EventQueueManager()
        {
            // Kill worker threads
            foreach (Thread EventQueueThread in new ArrayList(eventQueueThreads))
            {
                if (EventQueueThread != null && EventQueueThread.IsAlive == true)
                {
                    try
                    {
                        EventQueueThread.Abort();
                        EventQueueThread.Join();
                    }
                    catch (Exception)
                    {
                        //myScriptEngine.m_log.Info("[ScriptEngine]: EventQueueManager Exception killing worker thread: " + e.ToString());
                    }
                }
            }
            eventQueueThreads.Clear();
            // Todo: Clean up our queues
            eventQueue.Clear();
        }

        /// <summary>
        /// Queue processing thread loop
        /// </summary>
        private void EventQueueThreadLoop()
        {
            //myScriptEngine.m_log.Info("[ScriptEngine]: EventQueueManager Worker thread spawned");
            try
            {
                QueueItemStruct BlankQIS = new QueueItemStruct();
                while (true)
                {
                    try
                    {
                        QueueItemStruct QIS = BlankQIS;
                        bool GotItem = false;

                        if (eventQueue.Count == 0)
                        {
                            // Nothing to do? Sleep a bit waiting for something to do
                            Thread.Sleep(nothingToDoSleepms);
                        }
                        else
                        {
                            // Something in queue, process
                            //myScriptEngine.m_log.Info("[ScriptEngine]: Processing event for localID: " + QIS.localID + ", itemID: " + QIS.itemID + ", FunctionName: " + QIS.FunctionName);

                            // OBJECT BASED LOCK - TWO THREADS WORKING ON SAME OBJECT IS NOT GOOD
                            lock (queueLock)
                            {
                                GotItem = false;
                                for (int qc = 0; qc < eventQueue.Count; qc++)
                                {
                                    // Get queue item
                                    QIS = eventQueue.Dequeue();

                                    // Check if object is being processed by someone else
                                    if (TryLock(QIS.localID) == false)
                                    {
                                        // Object is already being processed, requeue it
                                        eventQueue.Enqueue(QIS);
                                    }
                                    else
                                    {
                                        // We have lock on an object and can process it
                                        GotItem = true;
                                        break;
                                    }
                                }
                            }

                            if (GotItem == true)
                            {
                                // Execute function
                                try
                                {
                                    m_ScriptEngine.m_ScriptManager.ExecuteEvent(QIS.localID, QIS.itemID,
                                                                                QIS.functionName, QIS.param);
                                }
                                catch (Exception e)
                                {
                                    // DISPLAY ERROR INWORLD
                                    string text = "Error executing script function \"" + QIS.functionName + "\":\r\n";
                                    if (e.InnerException != null)
                                    {
                                        // Send inner exception
                                        text += e.InnerException.Message.ToString();
                                    }
                                    else
                                    {
                                        // Send normal
                                        text += e.Message.ToString();
                                    }
                                    try
                                    {
                                        if (text.Length > 1500)
                                            text = text.Substring(0, 1500);
                                        IScriptHost m_host = m_ScriptEngine.World.GetSceneObjectPart(QIS.localID);
                                        //if (m_host != null)
                                        //{
                                        m_ScriptEngine.World.SimChat(Helpers.StringToField(text), ChatTypeEnum.Say, 0,
                                                                     m_host.AbsolutePosition, m_host.Name, m_host.UUID);
                                    }
                                    catch
                                    {
                                        //}
                                        //else
                                        //{
                                        // T oconsole
                                        Console.WriteLine("Unable to send text in-world:\r\n" + text);
                                    }
                                }
                                finally
                                {
                                    ReleaseLock(QIS.localID);
                                }
                            }
                        }
                    }
                    catch (ThreadAbortException tae)
                    {
                        throw tae;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception in EventQueueThreadLoop: " + e.ToString());
                    }
                }
            }
            catch (ThreadAbortException)
            {
                //myScriptEngine.m_log.Info("[ScriptEngine]: EventQueueManager Worker thread killed: " + tae.Message);
            }
        }

        /// <summary>
        /// Try to get a mutex lock on localID
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        private bool TryLock(uint localID)
        {
            lock (tryLockLock)
            {
                if (objectLocks.Contains(localID) == true)
                {
                    return false;
                }
                else
                {
                    objectLocks.Add(localID);
                    return true;
                }
            }
        }

        /// <summary>
        /// Release mutex lock on localID
        /// </summary>
        /// <param name="localID"></param>
        private void ReleaseLock(uint localID)
        {
            lock (tryLockLock)
            {
                if (objectLocks.Contains(localID) == true)
                {
                    objectLocks.Remove(localID);
                }
            }
        }

        /// <summary>
        /// Add event to event execution queue
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="FunctionName">Name of the function, will be state + "_event_" + FunctionName</param>
        /// <param name="param">Array of parameters to match event mask</param>
        public void AddToObjectQueue(uint localID, string FunctionName, object[] param)
        {
            // Determine all scripts in Object and add to their queue
            //myScriptEngine.m_log.Info("[ScriptEngine]: EventQueueManager Adding localID: " + localID + ", FunctionName: " + FunctionName);


            // Do we have any scripts in this object at all? If not, return
            if (m_ScriptEngine.m_ScriptManager.Scripts.ContainsKey(localID) == false)
            {
                //Console.WriteLine("Event \"" + FunctionName + "\" for localID: " + localID + ". No scripts found on this localID.");
                return;
            }

            Dictionary<LLUUID, LSL_BaseClass>.KeyCollection scriptKeys =
                m_ScriptEngine.m_ScriptManager.GetScriptKeys(localID);

            foreach (LLUUID itemID in scriptKeys)
            {
                // Add to each script in that object
                // TODO: Some scripts may not subscribe to this event. Should we NOT add it? Does it matter?
                AddToScriptQueue(localID, itemID, FunctionName, param);
            }
        }

        /// <summary>
        /// Add event to event execution queue
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="itemID"></param>
        /// <param name="FunctionName">Name of the function, will be state + "_event_" + FunctionName</param>
        /// <param name="param">Array of parameters to match event mask</param>
        public void AddToScriptQueue(uint localID, LLUUID itemID, string FunctionName, object[] param)
        {
            lock (queueLock)
            {
                // Create a structure and add data
                QueueItemStruct QIS = new QueueItemStruct();
                QIS.localID = localID;
                QIS.itemID = itemID;
                QIS.functionName = FunctionName;
                QIS.param = param;

                // Add it to queue
                eventQueue.Enqueue(QIS);
            }
        }
    }
}