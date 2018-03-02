// MIT License
//
// Unity FSM Code Generator - github.com/justonia/UnityFSMCodeGenerator
// 
// Copyright (c) 2018 Justin Larrabee
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnityFSMCodeGenerator
{
    // Stick this class on a GameObject and drag references to a MonoBehaviour 
    // implementing IHaveBaseFsm. If PlayMaker is installed and you link the 
    // prefab that generated the FSM into the tracking entries, you will be able
    // to view the FSM in the PlayMaker UI as well.
    public class FsmViewer : MonoBehaviour
    {
        private bool isDirty;

        [System.Serializable]
        public class TrackingPair
        {
            public PlayMakerCodeGenerator fsmPrefab;
            public MonoBehaviour fsmOwner;
            [NonSerialized] public BaseFsm targetFsm;
            [NonSerialized] public IFsmDebugSupport fsmDebug;
            #if PLAYMAKER
            [NonSerialized] public PlayMakerFSM view;
            #endif
        }

        public TrackingPair[] tracking;

        public delegate void RepaintAction();
        public event RepaintAction WantRepaint;

        private void OnEnable()
        {
            // Don't want this crap to run unless we're in the editor
            #if UNITY_EDITOR
            foreach (var pair in tracking) {
                if (pair.fsmOwner == null || !(pair.fsmOwner is IHaveBaseFsm)) {
                    continue;
                }
                StartCoroutine(Track(pair));
            }
            #endif
        }

        private void OnDisable()
        {
            #if UNITY_EDITOR
            StopAllCoroutines();
            foreach (var pair in tracking) {
                DestroyView(pair);
                pair.targetFsm = null;
                pair.fsmDebug = null;
            }
            #endif
        }

        #if UNITY_EDITOR
        private void Update()
        {
            if (isDirty && WantRepaint != null && Time.frameCount % 5 == 0) {
                WantRepaint();
                isDirty = false;
            }
        }
        #endif

        private IEnumerator Track(TrackingPair pair)
        {
            var haveFsm = pair.fsmOwner as IHaveBaseFsm;
            
            pair.targetFsm = haveFsm.BaseFsm;
            if (pair.targetFsm == null) {
                yield return new WaitUntil(() => haveFsm.BaseFsm != null);
                pair.targetFsm = haveFsm.BaseFsm;
            }

            pair.fsmDebug = pair.targetFsm as IFsmDebugSupport;
            if (pair.fsmDebug == null) {
                pair.targetFsm = null;
                yield break;
            }

            while (true) {
                var currentFsmState = pair.fsmDebug.State;
                isDirty = true;
                
                #if PLAYMAKER
                if (pair.view != null) { 
                    pair.view.SetState(currentFsmState);
                }
                #endif

                while (currentFsmState == pair.fsmDebug.State) {
                    #if PLAYMAKER
                    if (pair.view != null) {
                        pair.view.Fsm.Update();
                    }
                    #endif
                    yield return null;
                }
            }
        }

        public bool CanShowView(TrackingPair pair)
        {
            #if UNITY_EDITOR && PLAYMAKER
            return pair.targetFsm != null && pair.fsmPrefab != null && pair.fsmDebug != null;
            #else
            return false;
            #endif
        }

        public void ShowView(TrackingPair pair)
        {
            #if PLAYMAKER
            if (pair.view != null || !CanShowView(pair)) {
                return;
            }

            var go = Instantiate(pair.fsmPrefab.gameObject);
            go.transform.parent = transform;
            var fsm = go.GetComponent<PlayMakerFSM>();
            if (fsm == null) {
                Debug.LogFormat(gameObject, "fsmPrefab does not have a PlayMakerFSM on it");
                Destroy(go);
                return;
            }

            fsm.Fsm.ManualUpdate = true;

            // Sanity check that states line up
            var fsmStates = fsm.FsmStates.Select(s => s.Name).ToList();
            var intersectingStates = pair.fsmDebug.AllStates.Intersect(fsmStates).ToList();

            if (fsmStates.Count != pair.fsmDebug.AllStates.Count || intersectingStates.Count != pair.fsmDebug.AllStates.Count) {
                Debug.LogErrorFormat(gameObject, "Generated FSM and PlayMakerFSM do not have the same set of states");
                Destroy(go);
                return;
            }

            go.AddComponent<FsmViewerPrefabInstance>();

            pair.view = fsm;
            #endif
        }

        public void DestroyView(TrackingPair pair)
        {
            #if PLAYMAKER
            if (pair.view == null) {
                return;
            }

            Destroy(pair.view.gameObject);
            #endif
        }
    }
}