using System.Collections.Generic;
using ArtificeToolkit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SolarHarmony.Config
{
    [CustomEditor(typeof(GameConfigInstaller))]
    public class GameConfigInstallerEditor : UnityEditor.Editor
    {
        private VisualElement _contentContainer;
        private readonly List<ConfigUIElement> _configElements = new();
        private ArtificeDrawer _drawer;

        private class ConfigUIElement
        {
            public Foldout Foldout;
            public readonly List<PropertyUIElement> Properties = new();
        }

        private class PropertyUIElement
        {
            public VisualElement Container;
            public string Name;
        }

        public override VisualElement CreateInspectorGUI()
        {
            _drawer?.Dispose();
            _drawer = new ArtificeDrawer();

            var root = new VisualElement();

            var header = new VisualElement
            {
                style =
                {
                    backgroundColor = new StyleColor(new UnityEngine.Color(0.2f, 0.2f, 0.2f)),
                    paddingTop = 5,
                    paddingBottom = 5,
                    paddingLeft = 20,
                    paddingRight = 20,
                    marginLeft = -20,
                    marginRight = -20,
                    marginTop = -5,
                    marginBottom = 10
                }
            };

            var searchField = new ToolbarSearchField();
            searchField.style.flexGrow = 1;
            searchField.RegisterValueChangedCallback(evt => FilterSettings(evt.newValue));
            header.Add(searchField);
            root.Add(header);

            _contentContainer = new VisualElement();
            _contentContainer.style.paddingLeft = 5;
            _contentContainer.style.paddingRight = 5;
            root.Add(_contentContainer);

            var configsProp = serializedObject.FindProperty("Configs");
            if (configsProp == null || !configsProp.isArray)
                return root;

            for (int i = 0; i < configsProp.arraySize; i++)
            {
                var elementProp = configsProp.GetArrayElementAtIndex(i);
                var typeName = elementProp.managedReferenceFullTypename;
                if (string.IsNullOrEmpty(typeName)) continue;

                var parts = typeName.Split(' ');
                var fullClassName = parts.Length > 1 ? parts[1] : typeName;

                var displayName = fullClassName;

                int nestedDelimiterIndex = fullClassName.IndexOf('+');
                if (nestedDelimiterIndex < 0) nestedDelimiterIndex = fullClassName.IndexOf('/');

                if (nestedDelimiterIndex >= 0)
                {
                    var beforeNested = fullClassName.Substring(0, nestedDelimiterIndex);
                    var lastDot = beforeNested.LastIndexOf('.');
                    if (lastDot >= 0 && lastDot < beforeNested.Length - 1)
                    {
                        displayName = beforeNested.Substring(lastDot + 1);
                    }
                    else
                    {
                        displayName = beforeNested;
                    }
                }
                else
                {
                    var lastDot = fullClassName.LastIndexOf('.');
                    if (lastDot >= 0 && lastDot < fullClassName.Length - 1)
                    {
                        displayName = fullClassName.Substring(lastDot + 1);
                    }
                }

                var foldout = new Foldout { text = displayName, value = true };
                foldout.style.marginTop = 5;
                foldout.style.marginBottom = 5;
                foldout.Q<Label>().style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                var configElement = new ConfigUIElement { Foldout = foldout };

                bool hasVisibleChildren = false;

                var endProp = elementProp.GetEndProperty();
                elementProp.NextVisible(true);

                while (!SerializedProperty.EqualContents(elementProp, endProp))
                {
                    var propContainer = _drawer.CreatePropertyGUI(elementProp.Copy());

                    if (propContainer != null)
                    {
                        var propertyName = elementProp.displayName.ToLower();
                        foldout.Add(propContainer);
                        configElement.Properties.Add(new PropertyUIElement
                        {
                            Container = propContainer,
                            Name = propertyName
                        });
                        hasVisibleChildren = true;
                    }

                    elementProp.NextVisible(false);
                }

                if (hasVisibleChildren)
                {
                    _configElements.Add(configElement);
                    _contentContainer.Add(foldout);
                }
            }

            return root;
        }

        private void FilterSettings(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                foreach (var config in _configElements)
                {
                    config.Foldout.style.display = DisplayStyle.Flex;
                    foreach (var prop in config.Properties)
                    {
                        prop.Container.style.display = DisplayStyle.Flex;
                    }
                }
                return;
            }

            query = query.ToLower();

            foreach (var config in _configElements)
            {
                bool anyPropVisible = false;

                foreach (var prop in config.Properties)
                {
                    if (prop.Name.Contains(query) || config.Foldout.text.ToLower().Contains(query))
                    {
                        prop.Container.style.display = DisplayStyle.Flex;
                        anyPropVisible = true;
                    }
                    else
                    {
                        prop.Container.style.display = DisplayStyle.None;
                    }
                }

                config.Foldout.style.display = anyPropVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnDisable()
        {
            _drawer?.Dispose();
            _drawer = null;
        }
    }
}
