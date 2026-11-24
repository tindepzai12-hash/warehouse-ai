/*===============================================================================
Copyright (C) 2026 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.AI;
using Immersal.XR;
using Immersal.Samples.Util;
using TMPro;

namespace Immersal.Samples.Navigation
{
    [System.Serializable]
    public class NavigationEvent : UnityEvent<Transform>
    {
    }

    public class NavigationManager : MonoBehaviour
    {
        // Atlas adapter: preserve the sample UI/target/path prefabs while sharing one
        // validated metric route with the minimap and QR-A localization provider.
        public bool useAtlasValidatedRoute;
        // Opt-in for independent venue setup. Keep complete corners and avoid spline shortcuts.
        public bool requireCompleteNavMeshPath;
        public System.Action<IsNavigationTarget> atlasSelect;
        public System.Action atlasStop;
        private Vector3[] m_AtlasLastRoute;
        private float m_AtlasLastProgress=-1;
        public void ConfigureAtlas(System.Action<IsNavigationTarget> select,System.Action stop)
        {
            useAtlasValidatedRoute=true;atlasSelect=select;atlasStop=stop;m_managerInitialized=true;
            m_TargetsList.SetActive(false);m_StopNavigationButton.SetActive(false);
            m_TargetsListText.text="Chọn điểm A–E";m_TargetsListIcon.sprite=m_ShowListIcon;
        }
        public void ShowAtlasRoute(Transform mapRoot,Vector3[] points,bool visible,float progress=0)
        {
            if(!useAtlasValidatedRoute)return;
            if(mapRoot==null||points==null||points.Length<2||!visible)
            {if(m_navigationPathObject!=null)m_navigationPathObject.SetActive(false);m_StopNavigationButton.SetActive(false);return;}
            if(m_navigationPathObject==null)
            {m_navigationPathObject=Instantiate(m_navigationPathPrefab,mapRoot,false);m_navigationPath=m_navigationPathObject.GetComponent<NavigationPath>();m_AtlasLastRoute=null;}
            if(m_navigationPathObject.transform.parent!=mapRoot){m_navigationPathObject.transform.SetParent(mapRoot,false);m_AtlasLastRoute=null;}
            m_navigationPathObject.SetActive(true);m_StopNavigationButton.SetActive(true);
            if(!object.ReferenceEquals(points,m_AtlasLastRoute)||Mathf.Abs(progress-m_AtlasLastProgress)>.25f)
            {
                m_AtlasLastRoute=points;m_AtlasLastProgress=progress;var remaining=new List<Vector3>();float along=0;
                for(int i=1;i<points.Length;i++){float length=Vector3.Distance(points[i-1],points[i]);if(along+length>=progress){if(remaining.Count==0)remaining.Add(Vector3.Lerp(points[i-1],points[i],length>.001f?Mathf.Clamp01((progress-along)/length):0));remaining.Add(points[i]);}along+=length;}
                m_navigationPath.pathWidth=.20f;m_navigationPath.GenerateValidatedPolyline(remaining,Vector3.up);
            }
        }
        // Navigation Visualization references
        [Header("Visualization")]
        [SerializeField]
        private GameObject m_navigationPathPrefab = null;
        /*
        [SerializeField]
        private GameObject m_navigationArrowPrefab = null;
        */

        // UI Object references
        [Header("UI Objects")]
        [SerializeField]
        private GameObject m_TargetsList = null;
        [SerializeField]
        private Sprite m_ShowListIcon = null;
        [SerializeField]
        private Sprite m_SelectTargetIcon = null;
        [SerializeField]
        private Image m_TargetsListIcon = null;
        [SerializeField]
        private TextMeshProUGUI m_TargetsListText = null;
        [SerializeField]
        private GameObject m_StopNavigationButton = null;

        // Navigation Settings
        private enum NavigationMode { NavMesh, Graph};
        [Header("Settings")]
        [SerializeField]
        private NavigationMode m_navigationMode = NavigationMode.NavMesh;
        public bool inEditMode = false;
        /*
        [SerializeField]
        private bool m_showArrow = true;
        */
        [SerializeField]
        private float m_ArrivedDistanceThreshold = 1.0f;
        [SerializeField]
        private float m_pathWidth = 0.3f;
        [SerializeField]
        private float m_heightOffset = 0.5f;

        // Navigation State Events
        [Header("Events")]
        [SerializeField]
        private NavigationEvent onTargetFound = new NavigationEvent();
        [SerializeField]
        private NavigationEvent onTargetNotFound = new NavigationEvent();

        private XRSpace m_XRSpace = null;
        private bool m_managerInitialized = false;
        private bool m_navigationActive = false;
        private Transform m_targetTransform = null;
        private IsNavigationTarget m_NavigationTarget = null;
        private Transform m_playerTransform = null;
        private GameObject m_navigationPathObject = null;
        private NavigationPath m_navigationPath = null;

        [SerializeField]
        private NavigationGraphManager m_NavigationGraphManager = null;

        private enum NavigationState { NotNavigating, Navigating};
        private NavigationState m_navigationState = NavigationState.NotNavigating;

        private static NavigationManager instance = null;
        public static NavigationManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (instance == null && !Application.isPlaying)
                {
                    instance = FindFirstObjectByType<NavigationManager>();
                }
#endif
                if (instance == null)
                {
                    Debug.LogError("No NavigationManager instance found. Ensure one exists in the scene.");
                }
                return instance;
            }
        }

        public bool navigationActive
        {
            get { return m_navigationActive; }
        }

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            if (instance != this)
            {
                Debug.LogError("NavigationManager: There must be only one NavigationManager in a scene.");
                UnityEngine.Object.DestroyImmediate(this);
                return;
            }
        }

        private void Start()
        {
            if(useAtlasValidatedRoute)return;
            InitializeNavigationManager();

            if (m_managerInitialized)
            {
                m_TargetsListIcon.sprite = m_ShowListIcon;
                m_TargetsListText.text = "Show Navigation Targets";
            }
        }

        private void Update()
        {
            if(useAtlasValidatedRoute)return;
            if(m_managerInitialized && m_navigationState == NavigationState.Navigating)
            {
                TryToFindPath(m_NavigationTarget);
            }
        }

        public void InitializeNavigation(NavigationTargetListButton button)
        {
            if(useAtlasValidatedRoute){atlasSelect?.Invoke(button.targetObject.GetComponent<IsNavigationTarget>());return;}
            if (!m_managerInitialized)
            {
                Debug.LogWarning("NavigationManager: Navigation Manager not properly initialized.");
                return;
            }

            m_targetTransform = button.targetObject.transform;
            m_NavigationTarget = button.targetObject.GetComponent<IsNavigationTarget>();
            TryToFindPath(m_NavigationTarget);
        }

        public void TryToFindPath(IsNavigationTarget navigationTarget)
        {
            if (navigationTarget == null) return;
            m_NavigationTarget = navigationTarget;
            m_targetTransform = navigationTarget.transform;
            List<Vector3> corners;

            // Convert to Unity's world space coordinates to use NavMesh
            Vector3 startPosition = m_playerTransform.position;
            Vector3 targetPosition = navigationTarget.position;

            Vector3 delta = targetPosition - startPosition;
            float distanceToTarget = new Vector3(delta.x, delta.y, delta.z).magnitude;

            if (distanceToTarget < m_ArrivedDistanceThreshold)
            {
                m_navigationActive = false;

                m_navigationState = NavigationState.NotNavigating;
                UpdateNavigationUI(m_navigationState);

                DisplayArrivedNotification();
                return;
            }

            switch (m_navigationMode)
            {
                case NavigationMode.NavMesh:

                    if (!requireCompleteNavMeshPath)
                    {
                        startPosition = XRSpaceToUnity(m_XRSpace.transform, m_XRSpace.InitialPose, startPosition);
                        targetPosition = XRSpaceToUnity(m_XRSpace.transform, m_XRSpace.InitialPose, targetPosition);
                    }

                    corners = FindPathNavMesh(startPosition, targetPosition);
                    if (corners.Count >= 2)
                    {
                        m_navigationActive = true;

                        m_navigationState = NavigationState.Navigating;
                        UpdateNavigationUI(m_navigationState);

                        if (requireCompleteNavMeshPath) m_navigationPath.GenerateValidatedPolyline(corners, m_XRSpace.transform.up);
                        else m_navigationPath.GeneratePath(corners, m_XRSpace.transform.up);
                        m_navigationPath.pathWidth = m_pathWidth;
                    }
                    else
                    {
                        if (requireCompleteNavMeshPath)
                        {
                            m_navigationPath.ClearMesh();
                            m_navigationActive = false;
                            m_navigationState = NavigationState.NotNavigating;
                        }
                        NotificationManager.Instance.GenerateNotification("Path to target not found.");
                        UpdateNavigationUI(m_navigationState);
                    }
                    break;

                case NavigationMode.Graph:

                    corners = m_NavigationGraphManager.FindPath(startPosition, targetPosition);

                    if (corners.Count >= 2)
                    {
                        m_navigationActive = true;

                        m_navigationState = NavigationState.Navigating;
                        UpdateNavigationUI(m_navigationState);

                        m_navigationPath.GeneratePath(corners, m_XRSpace.transform.up);
                        m_navigationPath.pathWidth = m_pathWidth;
                    }
                    else
                    {
                        NotificationManager.Instance.GenerateNotification("Path to target not found.");
                        UpdateNavigationUI(m_navigationState);
                    }
                    break;
            }
        }

        private List<Vector3> FindPathNavMesh(Vector3 startPosition, Vector3 targetPosition)
        {
            NavMeshPath path = new NavMeshPath();
            List<Vector3> collapsedCorners = new List<Vector3>();

            if (requireCompleteNavMeshPath)
            {
                // Camera height is not a walkable-floor coordinate.
                if (!NavMesh.SamplePosition(startPosition, out var start, 2.2f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(targetPosition, out var target, 1.5f, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(start.position, target.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2)
                    return collapsedCorners;
                foreach (var point in path.corners)
                    collapsedCorners.Add(point + m_XRSpace.transform.up * m_heightOffset);
                return collapsedCorners;
            }

            if (NavMesh.CalculatePath(startPosition, targetPosition, NavMesh.AllAreas, path))
            {
                List<Vector3> corners = new List<Vector3>(path.corners);

                for (int i = 0; i < corners.Count; i++)
                {
                    corners[i] = corners[i] + new Vector3(0f, m_heightOffset, 0f);
                    corners[i] = UnityToXRSpace(m_XRSpace.transform, m_XRSpace.InitialPose, corners[i]);
                }

                for (int i = 0; i < corners.Count - 1; i++)
                {
                    Vector3 currentPoint = corners[i];
                    Vector3 nextPoint = corners[i + 1];
                    float threshold = 0.75f;

                    if (Vector3.Distance(currentPoint, nextPoint) > threshold)
                    {
                        collapsedCorners.Add(currentPoint);
                    }
                }

                collapsedCorners.Add(corners[corners.Count - 1]);
            }

            return collapsedCorners;
        }

        public void ToggleTargetsList()
        {
            if (!m_managerInitialized)
            {
                Debug.LogWarning("NavigationManager: Navigation Manager not properly initialized.");
                return;
            }

            if (m_TargetsList.activeInHierarchy)
            {
                m_TargetsList.SetActive(false);
                if (m_ShowListIcon != null && m_TargetsListIcon != null)
                {
                    m_TargetsListIcon.sprite = m_ShowListIcon;
                }
                if (m_TargetsListText != null)
                {
                    m_TargetsListText.text = useAtlasValidatedRoute?"Chọn điểm A–E":"Show Navigation Targets";
                }
            }
            else
            {
                m_TargetsList.SetActive(true);
                m_TargetsList.GetComponent<NavigationTargetListControl>().GenerateButtons();
                if (m_SelectTargetIcon != null && m_TargetsListIcon != null)
                {
                    m_TargetsListIcon.sprite = m_SelectTargetIcon;
                }
                if (m_TargetsListText != null)
                {
                    m_TargetsListText.text = useAtlasValidatedRoute?"Chọn điểm muốn tới":"Select Navigation Target";
                }
            }
        }

        public void ToggleEditMode()
        {
            inEditMode = !inEditMode;
        }

        public void DisplayPathNotFoundNotification()
        {
#if !(UNITY_STANDALONE)
            Handheld.Vibrate();
#endif
            NotificationManager.Instance.GenerateNotification("Path to target could not be found.");
            onTargetNotFound.Invoke(m_targetTransform);
        }

        public void DisplayArrivedNotification()
        {
#if !(UNITY_STANDALONE)
            Handheld.Vibrate();
#endif
            NotificationManager.Instance.GenerateNotification("You have arrived!");
            onTargetFound.Invoke(m_targetTransform);
        }

        public void StopNavigation()
        {
            if(useAtlasValidatedRoute){atlasStop?.Invoke();return;}
            m_navigationActive = false;

            m_navigationState = NavigationState.NotNavigating;
            UpdateNavigationUI(m_navigationState);

            if (NotificationManager.Instance != null)
                NotificationManager.Instance.GenerateNotification("Navigation stopped.");
        }

        private void UpdateNavigationUI(NavigationState navigationState)
        {
            switch(navigationState)
            {
                case NavigationState.NotNavigating:
                    if (m_StopNavigationButton != null) m_StopNavigationButton.SetActive(false);
                    if (m_navigationPathObject != null) m_navigationPathObject.SetActive(false);
                    break;
                case NavigationState.Navigating:
                    if (m_StopNavigationButton != null) m_StopNavigationButton.SetActive(true);
                    if (m_navigationPathObject != null) m_navigationPathObject.SetActive(true);
                    break;
            }
        }

        private void InitializeNavigationManager()
        {
            if (m_XRSpace == null)
            {
                m_XRSpace = FindFirstObjectByType<XRSpace>();

                if (m_XRSpace == null)
                {
                    Debug.LogWarning("NavigationManager: No XR Space found in scene, ensure one exists.");
                    return;
                }
            }

            m_NavigationGraphManager = GetComponent<NavigationGraphManager>();
            if (m_NavigationGraphManager == null)
            {
                Debug.LogWarning("NavigationManager: Missing Navigation Graph Manager component.");
                return;
            }

            m_playerTransform = Camera.main.transform;
            if (m_playerTransform == null)
            {
                Debug.LogWarning("NavigationManager: Could not find the main camera. Do you have the MainCamera tag applied?");
                return;
            }

            if (m_navigationPathPrefab == null)
            {
                Debug.LogWarning("NavigationManager: Missing navigation path object reference.");
                return;
            }

            if(m_navigationPathPrefab != null)
            {
                if (m_navigationPathObject == null)
                {
                    m_navigationPathObject = Instantiate(m_navigationPathPrefab);
                    m_navigationPathObject.SetActive(false);
                    m_navigationPath = m_navigationPathObject.GetComponent<NavigationPath>();
                }

                if(m_navigationPath == null)
                {
                    Debug.LogWarning("NavigationManager: NavigationPath component in Navigation path is missing.");
                    return;
                }
            }

            if (m_TargetsList == null)
            {
                Debug.LogWarning("NavigationManager: Navigation Targets List reference is missing.");
                return;
            }

            if (m_ShowListIcon == null)
            {
                Debug.LogWarning("NavigationManager: \"Show List\" icon is missing.");
                return;
            }

            if (m_SelectTargetIcon == null)
            {
                Debug.LogWarning("NavigationManager: \"Select Target\" icon is missing.");
                return;
            }

            if (m_TargetsListIcon == null)
            {
                Debug.LogWarning("NavigationManager: \"Targets List\" icon reference is missing.");
                return;
            }

            if (m_TargetsListText == null)
            {
                Debug.LogWarning("NavigationManager: \"Targets List\" text reference is missing.");
                return;
            }

            if (m_StopNavigationButton == null)
            {
                Debug.LogWarning("NavigationManager: Stop Navigation Button reference is missing.");
                return;
            }

            m_managerInitialized = true;
        }

        private Vector3 XRSpaceToUnity(Transform XRSpace, Matrix4x4 XRSpaceOffset, Vector3 pos)
        {
            Matrix4x4 m = XRSpace.worldToLocalMatrix;
            pos = m.MultiplyPoint(pos);
            pos = XRSpaceOffset.MultiplyPoint(pos);
            return pos;
        }

        private Vector3 XRSpaceToUnity(Transform XRSpace, Vector3 pos)
        {
            pos = XRSpaceToUnity(XRSpace, Matrix4x4.identity, pos);
            return pos;
        }

        private Vector3 UnityToXRSpace(Transform XRSpace, Matrix4x4 XRSpaceOffset, Vector3 pos)
        {
            pos = XRSpaceOffset.inverse.MultiplyPoint(pos);
            Matrix4x4 m = XRSpace.localToWorldMatrix;
            pos = m.MultiplyPoint(pos);
            return pos;
        }

        private Vector3 UnityToXRSpace(Transform XRSpace, Vector3 pos)
        {
            pos = UnityToXRSpace(XRSpace, Matrix4x4.identity, pos);
            return pos;
        }

        private void OnDestroy(){if(instance==this)instance=null;if(m_navigationPathObject!=null)Destroy(m_navigationPathObject);}

    }
}
