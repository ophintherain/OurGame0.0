using Spotlight.Shared;
using UnityEngine;

namespace Spotlight.Exploration
{
    public sealed class EndingScenePlayer : MonoBehaviour
    {
        [SerializeField] private StoryDefinition ending;
        [SerializeField] private Texture2D firstImage;
        [SerializeField] private Texture2D finalImage;
        private int line;
        private bool returning;

        public void Configure(StoryDefinition definition, Texture2D first, Texture2D final)
        {
            ending = definition;
            firstImage = first;
            finalImage = final;
        }

        private void Update()
        {
            if (returning || ending == null || ending.lines == null) return;
            if (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space) &&
                !Input.GetKeyDown(KeyCode.Return)) return;
            line++;
            if (line >= ending.lines.Length)
            {
                returning = SpotlightSceneTransition.TryLoad("Bootstrap");
            }
        }

        private void OnGUI()
        {
            if (ending == null || ending.lines == null || ending.lines.Length == 0 || returning) return;
            StoryLine current = ending.lines[Mathf.Clamp(line, 0, ending.lines.Length - 1)];
            Texture2D image = line == 4 ? finalImage : firstImage;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);
            if (line != 3 && image != null)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), image, ScaleMode.ScaleAndCrop);
            Rect panel = new Rect(Screen.width * 0.1f, Screen.height * 0.7f,
                Screen.width * 0.8f, Screen.height * 0.23f);
            GUI.Box(panel, GUIContent.none);
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(22, Screen.height / 30),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            style.normal.textColor = Color.white;
            GUI.Label(panel, current.text, style);
        }
    }
}
