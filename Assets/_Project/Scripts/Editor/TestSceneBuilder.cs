using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ResistenciaTahuantinsuyo.Runtime.AI;
using ResistenciaTahuantinsuyo.Runtime.Player;

namespace ResistenciaTahuantinsuyo.Editor
{
    public static class TestSceneBuilder
    {
        [MenuItem("Tahuantinsuyo/Build Test Scene (SCN_Test_AI)")]
        public static void BuildScene()
        {
            string scenePath = "Assets/_Project/Scenes/Test/SCN_Test_AI.unity";

            // Asegurar que la escena está abierta
            var scene = EditorSceneManager.OpenScene(scenePath);

            // Limpiar objetos anteriores
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                Object.DestroyImmediate(r);
            }

            // Cargar Sprites
            Sprite sprSquare = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/SPR_Square.png");
            Sprite sprCircle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/SPR_Circle.png");
            Sprite sprMarker = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/SPR_Marker.png");
            Sprite sprAlert = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/SPR_Alert.png");

            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer == -1) obstacleLayer = 8;

            // 1. Cámara Principal
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0, 0, -10);
            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9.0f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.20f, 0.22f, 0.18f);
            camObj.AddComponent<AudioListener>();

            // 2. Luz 2D Global
            GameObject lightObj = new GameObject("Global 2D Light");
            var light2D = lightObj.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.intensity = 1.0f;
            light2D.color = new Color(1.0f, 0.96f, 0.90f);

            // 3. Suelo
            GameObject groundObj = new GameObject("Ground");
            groundObj.transform.position = new Vector3(0, 0, 1);
            groundObj.transform.localScale = new Vector3(26, 18, 1);
            var groundSr = groundObj.AddComponent<SpriteRenderer>();
            groundSr.sprite = sprSquare;
            groundSr.color = new Color(0.85f, 0.80f, 0.70f); // Marfil / Arena
            groundSr.sortingOrder = -10;

            // 4. Muros Perimetrales
            GameObject boundaries = new GameObject("Boundaries");
            CreateWall("Wall_North", boundaries.transform, new Vector3(0, 9.5f, 0), new Vector3(28, 1, 1), obstacleLayer, sprSquare);
            CreateWall("Wall_South", boundaries.transform, new Vector3(0, -9.5f, 0), new Vector3(28, 1, 1), obstacleLayer, sprSquare);
            CreateWall("Wall_West", boundaries.transform, new Vector3(-13.5f, 0, 0), new Vector3(1, 20, 1), obstacleLayer, sprSquare);
            CreateWall("Wall_East", boundaries.transform, new Vector3(13.5f, 0, 0), new Vector3(1, 20, 1), obstacleLayer, sprSquare);

            // 5. Obstáculos Interiores para Sigilo
            GameObject obstacles = new GameObject("Obstacles");
            CreateWall("ENV_Wall_WestPillar", obstacles.transform, new Vector3(-4.5f, 0, 0), new Vector3(1.5f, 6.0f, 1), obstacleLayer, sprSquare);
            CreateWall("ENV_Wall_EastPillar", obstacles.transform, new Vector3(4.5f, 0, 0), new Vector3(1.5f, 6.0f, 1), obstacleLayer, sprSquare);
            GameObject centerWall = CreateWall("ENV_Wall_CenterBlock", obstacles.transform, new Vector3(0, 2.5f, 0), new Vector3(4.0f, 1.5f, 1), obstacleLayer, sprSquare);

            // Guardar prefab de muro
            PrefabUtility.SaveAsPrefabAsset(centerWall, "Assets/_Project/Prefabs/World/ENV_Wall.prefab");

            // 6. Rutas de Patrullaje
            GameObject routesParent = new GameObject("PatrolRoutes");

            // Ruta Oeste
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

            // Ruta Este
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

            // 7. Jugador (CHR_Player)
            GameObject playerObj = new GameObject("CHR_Player");
            playerObj.tag = "Player";
            playerObj.transform.position = new Vector3(0, -5f, 0);

            var playerSr = playerObj.AddComponent<SpriteRenderer>();
            playerSr.sprite = sprCircle;
            playerSr.color = new Color(0.26f, 0.32f, 0.43f); // #43526D Índigo textil
            playerSr.sortingOrder = 5;

            var playerCol = playerObj.AddComponent<CircleCollider2D>();
            playerCol.radius = 0.45f;

            var playerRb = playerObj.AddComponent<Rigidbody2D>();
            playerRb.gravityScale = 0;
            playerRb.freezeRotation = true;
            playerRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            playerObj.AddComponent<PlayerController>();

            GameObject playerMarker = new GameObject("OrientationMarker");
            playerMarker.transform.parent = playerObj.transform;
            playerMarker.transform.localPosition = new Vector3(0, 0.45f, 0);
            playerMarker.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            var pMarkerSr = playerMarker.AddComponent<SpriteRenderer>();
            pMarkerSr.sprite = sprMarker;
            pMarkerSr.color = new Color(0.91f, 0.86f, 0.78f); // #E7DCC8 Marfil
            pMarkerSr.sortingOrder = 6;

            // Guardar prefab de jugador
            PrefabUtility.SaveAsPrefabAsset(playerObj, "Assets/_Project/Prefabs/Player/CHR_Player.prefab");

            // 8. Enemigos (CHR_SpanishGuard)
            var guard1 = BuildGuard("CHR_SpanishGuard_01", new Vector3(-8f, -5f, 0), routeWest, playerObj.transform, sprCircle, sprMarker, sprAlert, obstacleLayer);
            var guard2 = BuildGuard("CHR_SpanishGuard_02", new Vector3(8f, 5f, 0), routeEast, playerObj.transform, sprCircle, sprMarker, sprAlert, obstacleLayer);

            // Guardar prefab de enemigo
            PrefabUtility.SaveAsPrefabAsset(guard1, "Assets/_Project/Prefabs/Enemies/CHR_SpanishGuard.prefab");

            // Guardar escena
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green><b>[Resistencia del Tahuantinsuyo]</b> Escena SCN_Test_AI y prefabs generados con éxito.</color>");
        }

        private static GameObject CreateWall(string name, Transform parent, Vector3 pos, Vector3 scale, int layer, Sprite sprite)
        {
            GameObject wall = new GameObject(name);
            wall.transform.parent = parent;
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.layer = layer;

            var sr = wall.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.42f, 0.31f, 0.23f); // #6B4F3A Marrón tierra
            sr.sortingOrder = 0;

            wall.AddComponent<BoxCollider2D>();
            return wall;
        }

        private static GameObject BuildGuard(string name, Vector3 pos, PatrolRoute route, Transform playerTransform, Sprite sprBody, Sprite sprMarker, Sprite sprAlert, int obstacleLayer)
        {
            GameObject guard = new GameObject(name);
            guard.transform.position = pos;

            var guardSr = guard.AddComponent<SpriteRenderer>();
            guardSr.sprite = sprBody;
            guardSr.color = new Color(0.60f, 0.36f, 0.24f); // #9A5B3E Arcilla / Terracota
            guardSr.sortingOrder = 5;

            var guardCol = guard.AddComponent<CircleCollider2D>();
            guardCol.radius = 0.45f;

            var guardRb = guard.AddComponent<Rigidbody2D>();
            guardRb.gravityScale = 0;
            guardRb.freezeRotation = true;
            guardRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var perception = guard.AddComponent<EnemyPerception>();
            var soPerc = new SerializedObject(perception);
            soPerc.FindProperty("viewDistance").floatValue = 6.5f;
            soPerc.FindProperty("viewAngle").floatValue = 90f;
            soPerc.FindProperty("obstacleMask").intValue = 1 << obstacleLayer;
            soPerc.FindProperty("targetMask").intValue = 1 << LayerMask.NameToLayer("Default");
            soPerc.FindProperty("targetTag").stringValue = "Player";
            soPerc.ApplyModifiedProperties();

            // Marcador de orientación
            GameObject gMarker = new GameObject("OrientationMarker");
            gMarker.transform.parent = guard.transform;
            gMarker.transform.localPosition = new Vector3(0, 0.45f, 0);
            gMarker.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            var gMarkerSr = gMarker.AddComponent<SpriteRenderer>();
            gMarkerSr.sprite = sprMarker;
            gMarkerSr.color = new Color(0.42f, 0.31f, 0.23f); // #6B4F3A
            gMarkerSr.sortingOrder = 6;

            // Indicador de Alerta
            GameObject alertObj = new GameObject("AlertIndicator");
            alertObj.transform.parent = guard.transform;
            alertObj.transform.localPosition = new Vector3(0, 0.85f, 0);
            alertObj.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            var alertSr = alertObj.AddComponent<SpriteRenderer>();
            alertSr.sprite = sprAlert;
            alertSr.color = new Color(0.66f, 0.25f, 0.21f); // #A94136 Rojo alerta
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
            soCtrl.ApplyModifiedProperties();

            return guard;
        }
    }
}
