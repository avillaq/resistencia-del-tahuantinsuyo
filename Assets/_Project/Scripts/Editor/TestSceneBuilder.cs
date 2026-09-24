using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using ResistenciaTahuantinsuyo.Runtime.AI;
using ResistenciaTahuantinsuyo.Runtime.Audio;
using ResistenciaTahuantinsuyo.Runtime.Combat;
using ResistenciaTahuantinsuyo.Runtime.Gameplay;
using ResistenciaTahuantinsuyo.Runtime.Player;
using ResistenciaTahuantinsuyo.Runtime.UI;

namespace ResistenciaTahuantinsuyo.Editor
{
    public static class TestSceneBuilder
    {
        [MenuItem("Tahuantinsuyo/Build Test Scene (SCN_Test_AI)")]
        public static void BuildScene()
        {
            string scenePath = "Assets/_Project/Scenes/Test/SCN_Test_AI.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);

            // Asegurar que el AudioMixer existe y está configurado según las 4 buenas prácticas
            AudioMixerBuilder.BuildAudioMixer();

            // Limpiar objetos anteriores
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                Object.DestroyImmediate(r);
            }

            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer == -1) obstacleLayer = 8;

            // 1. Cargar Assets de Sprites y Tiles
            Sprite sprPlayer = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Player/hero1.png");
            Sprite sprGuard = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Enemies/guard_1.png");
            Sprite sprAlert = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/SPR_Alert.png");
            Sprite sprMarker = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/SPR_Marker.png");

            Sprite sprQuipu = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/CulturalObjects/SPR_Quipu.png");
            Sprite sprHuaco = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/CulturalObjects/SPR_Huaco.png");
            Sprite sprHerbs = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Props/SPR_HealingHerbs.png");
            Sprite sprExitDoor = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Environments/SPR_ExitDoor.png");

            Tile tileFloor = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tilemaps/Tiles/Dungeon/TILE_dungeon_2_2.asset");
            Tile tileWallTop = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tilemaps/Tiles/Dungeon/TILE_dungeon_1_4.asset");
            Tile tileWallFace = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tilemaps/Tiles/Dungeon/TILE_dungeon_1_3.asset");
            Tile tileWallBottom = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tilemaps/Tiles/Dungeon/TILE_dungeon_1_0.asset");

            if (tileFloor != null) { tileFloor.colliderType = Tile.ColliderType.None; EditorUtility.SetDirty(tileFloor); }
            if (tileWallTop != null) { tileWallTop.colliderType = Tile.ColliderType.Grid; EditorUtility.SetDirty(tileWallTop); }
            if (tileWallFace != null) { tileWallFace.colliderType = Tile.ColliderType.Grid; EditorUtility.SetDirty(tileWallFace); }
            if (tileWallBottom != null) { tileWallBottom.colliderType = Tile.ColliderType.Grid; EditorUtility.SetDirty(tileWallBottom); }
            AssetDatabase.SaveAssets();

            // 2. Cámara Principal
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0, 0, -10);
            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9.0f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.12f);
            camObj.AddComponent<AudioListener>();

            // 3. Luz 2D Global (URP 2D)
            GameObject lightObj = new GameObject("Global 2D Light");
            var light2D = lightObj.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.intensity = 1.15f;
            light2D.color = new Color(1.0f, 0.97f, 0.92f);

            // 4. AudioManager con fuentes y enrutamiento preconfigurados
            GameObject audioMgrObj = new GameObject("AudioManager");
            var audioMgr = audioMgrObj.AddComponent<AudioManager>();

            var srcMusicA = audioMgrObj.AddComponent<AudioSource>();
            var srcMusicB = audioMgrObj.AddComponent<AudioSource>();
            var srcAmb = audioMgrObj.AddComponent<AudioSource>();
            var srcSfx = audioMgrObj.AddComponent<AudioSource>();
            var srcSteps = audioMgrObj.AddComponent<AudioSource>();
            var srcUi = audioMgrObj.AddComponent<AudioSource>();

            srcMusicA.playOnAwake = false;
            srcMusicA.loop = true;
            srcMusicB.playOnAwake = false;
            srcMusicB.loop = true;
            srcAmb.playOnAwake = false;
            srcAmb.loop = true;
            srcSfx.playOnAwake = false;
            srcSteps.playOnAwake = false;
            srcUi.playOnAwake = false;

            SetupAudioManagerClips(audioMgr, srcMusicA, srcMusicB, srcAmb, srcSfx, srcSteps, srcUi);

            // 5. ScoreManager (Gestor de Puntajes y Objetivos)
            GameObject scoreObj = new GameObject("ScoreManager");
            var scoreMgr = scoreObj.AddComponent<ScoreManager>();
            scoreMgr.SetTotalCulturalObjects(2);

            // 6. Grid y Tilemaps
            GameObject gridObj = new GameObject("Grid");
            var grid = gridObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            GameObject groundObj = new GameObject("Tilemap_Ground");
            groundObj.transform.parent = gridObj.transform;
            groundObj.transform.localPosition = Vector3.zero;
            var groundTilemap = groundObj.AddComponent<Tilemap>();
            var groundRenderer = groundObj.AddComponent<TilemapRenderer>();
            groundRenderer.sortingOrder = -10;

            int minX = -13, maxX = 12;
            int minY = -9, maxY = 8;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    groundTilemap.SetTile(new Vector3Int(x, y, 0), tileFloor);
                }
            }

            GameObject obstaclesObj = new GameObject("Tilemap_Obstacles");
            obstaclesObj.transform.parent = gridObj.transform;
            obstaclesObj.transform.localPosition = Vector3.zero;
            obstaclesObj.layer = obstacleLayer;

            var obsTilemap = obstaclesObj.AddComponent<Tilemap>();
            var obsRenderer = obstaclesObj.AddComponent<TilemapRenderer>();
            obsRenderer.sortingOrder = 0;

            var tilemapCollider = obstaclesObj.AddComponent<TilemapCollider2D>();
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

            var compositeCollider = obstaclesObj.AddComponent<CompositeCollider2D>();
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var obsRb = obstaclesObj.GetComponent<Rigidbody2D>();
            obsRb.bodyType = RigidbodyType2D.Static;

            void PaintWallRect(int startX, int startY, int width, int height)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    for (int y = startY; y < startY + height; y++)
                    {
                        Tile t = (y == startY + height - 1 && tileWallTop != null) ? tileWallTop :
                                 (y == startY && tileWallBottom != null) ? tileWallBottom : tileWallFace;
                        obsTilemap.SetTile(new Vector3Int(x, y, 0), t ?? tileFloor);
                    }
                }
            }

            // Perímetro exterior
            PaintWallRect(minX, maxY, maxX - minX + 1, 1);
            PaintWallRect(minX, minY, maxX - minX + 1, 1);
            PaintWallRect(minX, minY + 1, 1, maxY - minY - 1);
            PaintWallRect(maxX, minY + 1, 1, maxY - minY - 1);

            // Muros y pilares de sigilo interiores
            PaintWallRect(-5, -2, 2, 5);
            PaintWallRect(4, -2, 2, 5);
            PaintWallRect(-1, 2, 2, 2);

            tilemapCollider.ProcessTilemapChanges();
            compositeCollider.GenerateGeometry();

            // 7. Rutas de Patrullaje
            GameObject routesParent = new GameObject("PatrolRoutes");

            GameObject routeWestObj = new GameObject("PatrolRoute_West");
            routeWestObj.transform.parent = routesParent.transform;
            var routeWest = routeWestObj.AddComponent<PatrolRoute>();
            Vector3[] wpsWest = new Vector3[] {
                new Vector3(-8f, -5f, 0),
                new Vector3(-8f, 5f, 0),
                new Vector3(-2f, 5f, 0),
                new Vector3(-2f, -5f, 0)
            };
            for (int i = 0; i < wpsWest.Length; i++)
            {
                GameObject wp = new GameObject("WP_" + (i + 1));
                wp.transform.parent = routeWestObj.transform;
                wp.transform.position = wpsWest[i];
            }

            GameObject routeEastObj = new GameObject("PatrolRoute_East");
            routeEastObj.transform.parent = routesParent.transform;
            var routeEast = routeEastObj.AddComponent<PatrolRoute>();
            Vector3[] wpsEast = new Vector3[] {
                new Vector3(2f, -5f, 0),
                new Vector3(8f, -5f, 0),
                new Vector3(8f, 5f, 0),
                new Vector3(2f, 5f, 0)
            };
            for (int i = 0; i < wpsEast.Length; i++)
            {
                GameObject wp = new GameObject("WP_" + (i + 1));
                wp.transform.parent = routeEastObj.transform;
                wp.transform.position = wpsEast[i];
            }

            // 8. Jugador con Health y PlayerController
            GameObject playerObj = new GameObject("CHR_Player");
            playerObj.tag = "Player";
            playerObj.transform.position = new Vector3(0, -6f, 0);
            playerObj.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var playerSr = playerObj.AddComponent<SpriteRenderer>();
            playerSr.sprite = sprPlayer;
            playerSr.color = Color.white;
            playerSr.sortingOrder = 5;

            var playerCol = playerObj.AddComponent<CircleCollider2D>();
            playerCol.radius = 0.35f;

            var playerRb = playerObj.AddComponent<Rigidbody2D>();
            playerRb.gravityScale = 0;
            playerRb.freezeRotation = true;
            playerRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var playerHealth = playerObj.AddComponent<Health>();
            playerHealth.SetMaxHealth(100);

            var playerCtrl = playerObj.AddComponent<PlayerController>();

            GameObject playerMarker = new GameObject("OrientationMarker");
            playerMarker.transform.parent = playerObj.transform;
            playerMarker.transform.localPosition = new Vector3(0, 0.45f, 0);
            playerMarker.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var pMarkerSr = playerMarker.AddComponent<SpriteRenderer>();
            pMarkerSr.sprite = sprMarker;
            pMarkerSr.color = new Color(0.91f, 0.86f, 0.78f, 0.6f);
            pMarkerSr.sortingOrder = 6;

            PrefabUtility.SaveAsPrefabAsset(playerObj, "Assets/_Project/Prefabs/Player/CHR_Player.prefab");

            // 9. Enemigos con Health y Melee Attack
            var guard1 = BuildGuard("CHR_SpanishGuard_01", new Vector3(-8f, -5f, 0), routeWest, playerObj.transform, sprGuard, sprMarker, sprAlert, obstacleLayer);
            var guard2 = BuildGuard("CHR_SpanishGuard_02", new Vector3(8f, 5f, 0), routeEast, playerObj.transform, sprGuard, sprMarker, sprAlert, obstacleLayer);

            PrefabUtility.SaveAsPrefabAsset(guard1, "Assets/_Project/Prefabs/Enemies/CHR_SpanishGuard.prefab");

            // 10. Objetos Culturales Recuperables (+Puntajes) con Sprites representativos
            BuildCulturalPickup("OBJ_Cultural_Quipu", "OBJ_Quipu_01", "Quipu Administrativo", new Vector3(-7f, 4f, 0), sprQuipu ?? sprMarker, Color.white);
            BuildCulturalPickup("OBJ_Cultural_Huaco", "OBJ_Huaco_01", "Huaco Ceremonial", new Vector3(7f, -3f, 0), sprHuaco ?? sprMarker, Color.white);

            // 11. Depósito Medicinal de Hierbas Andinas (+Vida)
            BuildHealingPickup("PICKUP_HealingHerbs", new Vector3(-7f, -1f, 0), sprHerbs ?? sprMarker, Color.white);

            // 12. Zona de Evacuación / Salida Segura (Norte)
            BuildExitZone("ZONE_MissionExit", new Vector3(0f, 6.5f, 0), sprExitDoor ?? sprMarker);

            // 13. EventSystem con InputSystemUIInputModule para botones y UI interactiva
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();

            // 14. HUD con UI Toolkit (UIDocument + HUDController + PanelSettings)
            GameObject hudObj = new GameObject("UI_HUD");
            var uiDoc = hudObj.AddComponent<UIDocument>();
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/UXML/HUD.uxml");
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/UI/PanelSettings_Game.asset");
            uiDoc.visualTreeAsset = visualTree;
            uiDoc.panelSettings = panelSettings;

            var hudCtrl = hudObj.AddComponent<HUDController>();
            hudCtrl.SetPlayerHealth(playerHealth);

            // 15. Guardar Escena
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green><b>[Resistencia del Tahuantinsuyo]</b> Escena SCN_Test_AI generada exitosamente con PanelSettings, EventSystem, Vida, Puntajes, Reliquias y HUD.</color>");
        }

        private static void BuildCulturalPickup(string gameObjectName, string id, string name, Vector3 pos, Sprite spr, Color tint)
        {
            GameObject obj = new GameObject(gameObjectName);
            obj.transform.position = pos;
            obj.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.color = tint;
            sr.sortingOrder = 4;

            var col = obj.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
            col.isTrigger = true;

            var pickup = obj.AddComponent<CulturalObjectPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("objectId").stringValue = id;
            so.FindProperty("objectName").stringValue = name;
            so.FindProperty("scoreValue").intValue = 500;
            so.ApplyModifiedProperties();
        }

        private static void BuildHealingPickup(string gameObjectName, Vector3 pos, Sprite spr, Color tint)
        {
            GameObject obj = new GameObject(gameObjectName);
            obj.transform.position = pos;
            obj.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.color = tint;
            sr.sortingOrder = 4;

            var col = obj.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
            col.isTrigger = true;

            var heal = obj.AddComponent<HealingPickup>();
            var so = new SerializedObject(heal);
            so.FindProperty("healAmount").intValue = 35;
            so.ApplyModifiedProperties();
        }

        private static void BuildExitZone(string gameObjectName, Vector3 pos, Sprite spr)
        {
            GameObject obj = new GameObject(gameObjectName);
            obj.transform.position = pos;
            obj.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.color = Color.white;
            sr.sortingOrder = 1;

            var col = obj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.2f, 1.2f);
            col.isTrigger = true;

            var exit = obj.AddComponent<MissionExitZone>();
            exit.RequireAllCulturalObjects = true;
        }

        private static void SetupAudioManagerClips(
            AudioManager mgr,
            AudioSource srcMusicA,
            AudioSource srcMusicB,
            AudioSource srcAmb,
            AudioSource srcSfx,
            AudioSource srcSteps,
            AudioSource srcUi)
        {
            var so = new SerializedObject(mgr);

            so.FindProperty("musicSourceA").objectReferenceValue = srcMusicA;
            so.FindProperty("musicSourceB").objectReferenceValue = srcMusicB;
            so.FindProperty("ambienceSource").objectReferenceValue = srcAmb;
            so.FindProperty("sfxSource").objectReferenceValue = srcSfx;
            so.FindProperty("footstepSource").objectReferenceValue = srcSteps;
            so.FindProperty("uiSource").objectReferenceValue = srcUi;

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Project/Audio/AudioMixer_Master.mixer");
            if (mixer != null)
            {
                so.FindProperty("audioMixer").objectReferenceValue = mixer;

                var musicGroups = mixer.FindMatchingGroups("Music");
                if (musicGroups.Length > 0)
                {
                    so.FindProperty("musicGroup").objectReferenceValue = musicGroups[0];
                    srcMusicA.outputAudioMixerGroup = musicGroups[0];
                    srcMusicB.outputAudioMixerGroup = musicGroups[0];
                }

                var ambGroups = mixer.FindMatchingGroups("Ambience");
                if (ambGroups.Length > 0)
                {
                    so.FindProperty("ambienceGroup").objectReferenceValue = ambGroups[0];
                    srcAmb.outputAudioMixerGroup = ambGroups[0];
                }

                var sfxGroups = mixer.FindMatchingGroups("SFX");
                if (sfxGroups.Length > 0)
                {
                    so.FindProperty("sfxGroup").objectReferenceValue = sfxGroups[0];
                    srcSfx.outputAudioMixerGroup = sfxGroups[0];
                }

                var footstepGroups = mixer.FindMatchingGroups("Footsteps");
                if (footstepGroups.Length > 0)
                {
                    so.FindProperty("footstepsGroup").objectReferenceValue = footstepGroups[0];
                    srcSteps.outputAudioMixerGroup = footstepGroups[0];
                }

                var uiGroups = mixer.FindMatchingGroups("UI");
                if (uiGroups.Length > 0)
                {
                    so.FindProperty("uiGroup").objectReferenceValue = uiGroups[0];
                    srcUi.outputAudioMixerGroup = uiGroups[0];
                }

                var snapExploration = mixer.FindSnapshot("Snapshot_Exploration");
                if (snapExploration != null) so.FindProperty("snapshotExploration").objectReferenceValue = snapExploration;

                var snapTension = mixer.FindSnapshot("Snapshot_Tension");
                if (snapTension != null) so.FindProperty("snapshotTension").objectReferenceValue = snapTension;
            }

            // Music
            so.FindProperty("musicMenu").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Music/MUS_Menu.ogg");
            so.FindProperty("musicExploration").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Music/MUS_Exploration.ogg");
            so.FindProperty("musicTension").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Music/MUS_Tension.ogg");
            so.FindProperty("musicMissionComplete").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Music/MUS_MissionComplete.ogg");
            so.FindProperty("musicMissionFailed").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Music/MUS_MissionFailed.ogg");

            // Ambience
            so.FindProperty("ambienceAndes").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Ambience/AMB_Andes.wav");
            so.FindProperty("ambienceCamp").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Ambience/AMB_Camp.wav");
            so.FindProperty("ambienceCoast").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Ambience/AMB_Coast.wav");
            so.FindProperty("ambienceSettlement").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Ambience/AMB_Settlement.wav");

            // Player SFX
            so.FindProperty("sfxPlayerInteract").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Player/SFX_Player_Interact.wav");
            so.FindProperty("sfxPlayerDamage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Enemy/SFX_Enemy_LostSight.wav");
            AssignClipArray(so.FindProperty("sfxPlayerStepStone"), "Assets/_Project/Audio/SFX/Player/SFX_Player_Step_Stone_v", 4);
            AssignClipArray(so.FindProperty("sfxPlayerStepDirt"), "Assets/_Project/Audio/SFX/Player/SFX_Player_Step_Dirt_v", 4);

            // Enemy SFX
            so.FindProperty("sfxEnemyAlert").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Enemy/SFX_Enemy_Alert.wav");
            so.FindProperty("sfxEnemyLostSight").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Enemy/SFX_Enemy_LostSight.wav");
            AssignClipArray(so.FindProperty("sfxEnemyStepArmor"), "Assets/_Project/Audio/SFX/Enemy/SFX_Enemy_Step_Armor_v", 4);

            // UI SFX
            so.FindProperty("sfxUiSelect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/UI/SFX_UI_Select.wav");
            so.FindProperty("sfxUiPause").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/UI/SFX_UI_Pause.wav");
            so.FindProperty("sfxUiResume").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/UI/SFX_UI_Resume.wav");
            so.FindProperty("sfxObjectRecovered").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/UI/SFX_Object_Recovered.wav");

            so.ApplyModifiedProperties();
        }

        private static void AssignClipArray(SerializedProperty prop, string basePathPrefix, int count)
        {
            prop.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                string path = basePathPrefix + (i + 1) + ".wav";
                prop.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }

        private static GameObject BuildGuard(string name, Vector3 pos, PatrolRoute route, Transform playerTransform, Sprite sprBody, Sprite sprMarker, Sprite sprAlert, int obstacleLayer)
        {
            GameObject guard = new GameObject(name);
            guard.transform.position = pos;
            guard.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var guardSr = guard.AddComponent<SpriteRenderer>();
            guardSr.sprite = sprBody;
            guardSr.color = Color.white;
            guardSr.sortingOrder = 5;

            var guardCol = guard.AddComponent<CircleCollider2D>();
            guardCol.radius = 0.35f;

            var guardRb = guard.AddComponent<Rigidbody2D>();
            guardRb.gravityScale = 0;
            guardRb.freezeRotation = true;
            guardRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var guardHealth = guard.AddComponent<Health>();
            guardHealth.SetMaxHealth(50);

            var perception = guard.AddComponent<EnemyPerception>();
            var soPerc = new SerializedObject(perception);
            soPerc.FindProperty("viewDistance").floatValue = 6.5f;
            soPerc.FindProperty("viewAngle").floatValue = 90f;
            soPerc.FindProperty("obstacleMask").intValue = 1 << obstacleLayer;
            soPerc.FindProperty("targetMask").intValue = 1 << LayerMask.NameToLayer("Default");
            soPerc.FindProperty("targetTag").stringValue = "Player";
            soPerc.ApplyModifiedProperties();

            GameObject gMarker = new GameObject("OrientationMarker");
            gMarker.transform.parent = guard.transform;
            gMarker.transform.localPosition = new Vector3(0, 0.45f, 0);
            gMarker.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var gMarkerSr = gMarker.AddComponent<SpriteRenderer>();
            gMarkerSr.sprite = sprMarker;
            gMarkerSr.color = new Color(0.66f, 0.25f, 0.21f, 0.6f);
            gMarkerSr.sortingOrder = 6;

            GameObject alertObj = new GameObject("AlertIndicator");
            alertObj.transform.parent = guard.transform;
            alertObj.transform.localPosition = new Vector3(0, 0.75f, 0);
            alertObj.transform.localScale = new Vector3(1.0f, 1.0f, 1f);
            var alertSr = alertObj.AddComponent<SpriteRenderer>();
            alertSr.sprite = sprAlert;
            alertSr.color = new Color(0.66f, 0.25f, 0.21f);
            alertSr.sortingOrder = 7;
            alertSr.enabled = false;

            var controller = guard.AddComponent<EnemyController>();
            var soCtrl = new SerializedObject(controller);
            soCtrl.FindProperty("patrolRoute").objectReferenceValue = route;
            soCtrl.FindProperty("targetPlayer").objectReferenceValue = playerTransform;
            soCtrl.FindProperty("bodyRenderer").objectReferenceValue = guardSr;
            soCtrl.FindProperty("alertIndicatorRenderer").objectReferenceValue = alertSr;
            soCtrl.FindProperty("wanderSpeed").floatValue = 2.0f;
            soCtrl.FindProperty("seekSpeed").floatValue = 3.5f;
            soCtrl.FindProperty("waypointWaitTime").floatValue = 1.0f;
            soCtrl.FindProperty("lostSightCooldown").floatValue = 1.5f;
            soCtrl.FindProperty("attackDamage").intValue = 25;
            soCtrl.FindProperty("attackDistance").floatValue = 0.85f;
            soCtrl.FindProperty("attackCooldown").floatValue = 1.0f;
            soCtrl.FindProperty("wanderColor").colorValue = Color.white;
            soCtrl.FindProperty("seekColor").colorValue = new Color(1.0f, 0.6f, 0.6f);
            soCtrl.ApplyModifiedProperties();

            return guard;
        }
    }
}