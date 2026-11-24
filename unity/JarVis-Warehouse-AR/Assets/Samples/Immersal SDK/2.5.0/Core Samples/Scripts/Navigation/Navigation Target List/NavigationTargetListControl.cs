/*===============================================================================
Copyright (C) 2026 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Immersal.Samples.Navigation
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(ScrollRect))]
    public class NavigationTargetListControl : MonoBehaviour
    {
        public bool atlasResponsiveLayout;
        [SerializeField]
        private GameObject m_ButtonTemplate = null;
        [SerializeField]
        private RectTransform m_ContentParent = null;
        [SerializeField]
        int m_MaxButtonsOnScreen = 4;
        private List<GameObject> m_Buttons = new List<GameObject>();

        public void GenerateButtons()
        {
            if (m_Buttons.Count > 0)
            {
                DestroyButtons();
            }
            
            // loops through all navigation categories
            foreach (KeyValuePair<NavigationTargets.NavigationCategory, List<GameObject>> entry in NavigationTargets.NavigationTargetsDict)
            {

                // loops through all targets in each category
                foreach (GameObject go in entry.Value)
                {
                    IsNavigationTarget isNavigationTarget = go.GetComponent<IsNavigationTarget>();
                    string targetName = isNavigationTarget.targetName;
                    Sprite icon = isNavigationTarget.icon;

                    GameObject button = Instantiate(m_ButtonTemplate, m_ContentParent);
                    m_Buttons.Add(button);
                    button.SetActive(true);
                    if(atlasResponsiveLayout){var r=button.GetComponent<RectTransform>();r.sizeDelta=new Vector2(320,52);foreach(var text in button.GetComponentsInChildren<TMPro.TMP_Text>(true))text.fontSize=16;}
                    button.name = string.Format("button {0}", targetName);

                    NavigationTargetListButton navigationTargetListButton = button.GetComponent<NavigationTargetListButton>();
                    navigationTargetListButton.SetText(targetName);
                    navigationTargetListButton.SetIcon(icon);
                    navigationTargetListButton.SetTarget(go);
                }
            }

            // calculate lists RectTransform size
            float x = atlasResponsiveLayout?340:m_ButtonTemplate.GetComponent<RectTransform>().sizeDelta.x;
            float y = (atlasResponsiveLayout?52:m_ButtonTemplate.GetComponent<RectTransform>().sizeDelta.y) * Mathf.Min(m_Buttons.Count, m_MaxButtonsOnScreen);
            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(x, y);

            ScrollToTop();
        }

        private void DestroyButtons()
        {
            foreach (GameObject button in m_Buttons)
            {
                button.SetActive(false);
                if(Application.isPlaying)Destroy(button);else DestroyImmediate(button);
            }
            m_Buttons.Clear();
        }

        private void ScrollToTop()
        {
            transform.GetComponent<ScrollRect>().normalizedPosition = new Vector2(0, 1);
        }
    }
}
