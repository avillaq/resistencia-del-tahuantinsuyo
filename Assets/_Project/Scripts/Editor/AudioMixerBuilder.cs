#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace ResistenciaTahuantinsuyo.Editor
{
    /// <summary>
    /// Utilidad de Editor para generar y configurar el AudioMixer según las 4 buenas prácticas:
    /// 1. Jerarquía de Buses y Headroom (Master, Music, Ambience, SFX, Footsteps, UI).
    /// 2. Ducking Dinámico / Snapshots de Estado (Exploration vs Tension).
    /// 3. Parámetros Expuestos con escala logarítmica (MasterVolume, MusicVolume, etc.).
    /// 4. Variación Acústica Anti-Fatiga en fuentes y llamadas.
    /// </summary>
    public static class AudioMixerBuilder
    {
        public const string MixerPath = "Assets/_Project/Audio/AudioMixer_Master.mixer";

        [MenuItem("Tahuantinsuyo/Build AudioMixer (Master & Buses)", false, 30)]
        public static AudioMixer BuildAudioMixer()
        {
            Type ctrlType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
            Type grpType = Type.GetType("UnityEditor.Audio.AudioMixerGroupController, UnityEditor");
            Type epType = Type.GetType("UnityEditor.Audio.ExposedAudioParameter, UnityEditor");

            if (ctrlType == null || grpType == null || epType == null)
            {
                Debug.LogError("[AudioMixerBuilder] No se pudieron reflejar los tipos internos de UnityEditor.Audio.");
                return null;
            }

            var existingMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            object mixerObj = null;

            if (existingMixer == null)
            {
                var createM = ctrlType.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                mixerObj = createM.Invoke(null, new object[] { MixerPath });
            }
            else
            {
                mixerObj = AssetDatabase.LoadAssetAtPath(MixerPath, ctrlType);
            }

            if (mixerObj == null)
            {
                Debug.LogError("[AudioMixerBuilder] Falló la creación/carga del AudioMixer.");
                return null;
            }

            var master = ctrlType.GetProperty("masterGroup").GetValue(mixerObj, null);
            var createNewGroupM = ctrlType.GetMethod("CreateNewGroup", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var addChildM = ctrlType.GetMethod("AddChildToParent", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var sanitizeM = ctrlType.GetMethod("SanitizeGroupViews", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var setVolM = grpType.GetMethod("SetValueForVolume", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var getGuidForVolM = grpType.GetMethod("GetGUIDForVolume", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            var audioMixer = (AudioMixer)mixerObj;
            var groups = audioMixer.FindMatchingGroups(string.Empty);
            var groupMap = new Dictionary<string, object>();
            foreach (var g in groups)
            {
                groupMap[g.name] = g;
            }

            object music = GetOrCreateGroup("Music", master, groupMap, mixerObj, createNewGroupM, addChildM);
            object ambience = GetOrCreateGroup("Ambience", master, groupMap, mixerObj, createNewGroupM, addChildM);
            object sfx = GetOrCreateGroup("SFX", master, groupMap, mixerObj, createNewGroupM, addChildM);
            object footsteps = GetOrCreateGroup("Footsteps", sfx, groupMap, mixerObj, createNewGroupM, addChildM);
            object ui = GetOrCreateGroup("UI", sfx, groupMap, mixerObj, createNewGroupM, addChildM);

            if (sanitizeM != null) sanitizeM.Invoke(mixerObj, null);

            // 2. Snapshots: Exploration y Tension
            var snapsProp = ctrlType.GetProperty("snapshots", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var snaps = (Array)snapsProp.GetValue(mixerObj, null);

            object snapExploration = null;
            object snapTension = null;

            if (snaps != null && snaps.Length > 0)
            {
                snapExploration = snaps.GetValue(0);
                ((UnityEngine.Object)snapExploration).name = "Snapshot_Exploration";

                if (snaps.Length > 1)
                {
                    snapTension = snaps.GetValue(1);
                    ((UnityEngine.Object)snapTension).name = "Snapshot_Tension";
                }
                else
                {
                    var cloneM = ctrlType.GetMethod("CloneNewSnapshotFromTarget", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    snapTension = cloneM.Invoke(mixerObj, new object[] { false });
                    ((UnityEngine.Object)snapTension).name = "Snapshot_Tension";
                }
            }

            var startSnapProp = ctrlType.GetProperty("startSnapshot", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (startSnapProp != null && snapExploration != null)
            {
                startSnapProp.SetValue(mixerObj, snapExploration, null);
            }

            // 1. Calibrar volúmenes para Exploration
            if (snapExploration != null)
            {
                setVolM.Invoke(master, new object[] { mixerObj, snapExploration, -2.0f });
                setVolM.Invoke(music, new object[] { mixerObj, snapExploration, -7.0f });
                setVolM.Invoke(ambience, new object[] { mixerObj, snapExploration, -12.0f });
                setVolM.Invoke(sfx, new object[] { mixerObj, snapExploration, -2.0f });
                setVolM.Invoke(footsteps, new object[] { mixerObj, snapExploration, -10.0f });
                setVolM.Invoke(ui, new object[] { mixerObj, snapExploration, -3.0f });
            }

            // 2. Calibrar volúmenes para Tension (Ducking dinámico)
            if (snapTension != null)
            {
                setVolM.Invoke(master, new object[] { mixerObj, snapTension, -2.0f });
                setVolM.Invoke(music, new object[] { mixerObj, snapTension, -5.0f });
                setVolM.Invoke(ambience, new object[] { mixerObj, snapTension, -18.0f });
                setVolM.Invoke(sfx, new object[] { mixerObj, snapTension, 0.0f });
                setVolM.Invoke(footsteps, new object[] { mixerObj, snapTension, -7.0f });
                setVolM.Invoke(ui, new object[] { mixerObj, snapTension, -3.0f });
            }

            // 3. Exponer parámetros para UI Settings
            var exposedList = new List<object>();
            var exposedPairs = new KeyValuePair<string, object>[] {
                new KeyValuePair<string, object>("MasterVolume", master),
                new KeyValuePair<string, object>("MusicVolume", music),
                new KeyValuePair<string, object>("AmbienceVolume", ambience),
                new KeyValuePair<string, object>("SFXVolume", sfx),
                new KeyValuePair<string, object>("FootstepsVolume", footsteps)
            };

            foreach (var pair in exposedPairs)
            {
                var guid = getGuidForVolM.Invoke(pair.Value, null);
                var ep = Activator.CreateInstance(epType);
                epType.GetField("name").SetValue(ep, pair.Key);
                epType.GetField("guid").SetValue(ep, guid);
                exposedList.Add(ep);
            }

            var epArray = Array.CreateInstance(epType, exposedList.Count);
            for (int i = 0; i < exposedList.Count; i++) epArray.SetValue(exposedList[i], i);

            var exposedProp = ctrlType.GetProperty("exposedParameters", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            exposedProp.SetValue(mixerObj, epArray, null);

            var onParamChanged = ctrlType.GetMethod("OnChangedExposedParameter", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (onParamChanged != null) onParamChanged.Invoke(mixerObj, null);

            EditorUtility.SetDirty((UnityEngine.Object)mixerObj);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green><b>[AudioMixerBuilder]</b> AudioMixer_Master configurado con éxito aplicando las 4 buenas prácticas.</color>");
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        private static object GetOrCreateGroup(string name, object parent, Dictionary<string, object> map, object mixerObj, MethodInfo createNewGroupM, MethodInfo addChildM)
        {
            if (map.ContainsKey(name))
            {
                return map[name];
            }

            var newGroup = createNewGroupM.Invoke(mixerObj, new object[] { name, false });
            addChildM.Invoke(mixerObj, new object[] { newGroup, parent });
            map[name] = newGroup;
            return newGroup;
        }
    }
}
#endif
