using UnityEditor;
using UnityEngine;

namespace PixelRender
{
    public static class SDFLightMenuExtension
    {
        [MenuItem("GameObject/SDFLight/Sphere Light", false, 0)]
        private static void CreateSphereLight()
        {
            CreateLight<SphereSDF>("Sphere Light");
        }
        
        [MenuItem("GameObject/SDFLight/Sphere Light", true, 0)]
        private static bool ValidateCreateSphereLight()
        {
            return true;
        }
        
        [MenuItem("GameObject/SDFLight/Box Light", false, 0)]
        private static void CreateBoxLight()
        {
            CreateLight<BoxSDF>("Box Light");
        }
        
        [MenuItem("GameObject/SDFLight/Box Light", true, 0)]
        private static bool ValidateCreateBoxLight()
        {
            return true;
        }
        
        [MenuItem("GameObject/SDFLight/Torus Light", false, 0)]
        private static void CreateTorusLight()
        {
            CreateLight<TorusSDF>("Torus Light");
        }
        
        [MenuItem("GameObject/SDFLight/Torus Light", true, 0)]
        private static bool ValidateCreateTorusLight()
        {
            return true;
        }
        
        [MenuItem("GameObject/SDFLight/Capsule Light", false, 0)]
        private static void CreateCapsuleLight()
        {
            CreateLight<CapsuleSDF>("Capsule Light");
        }
        
        [MenuItem("GameObject/SDFLight/Capsule Light", true, 0)]
        private static bool ValidateCreateCapsuleLight()
        {
            return true;
        }
        
        private static void CreateLight<T>(string objName) where T : SDFLight
        {
            GameObject newObject = new GameObject(objName);
            newObject.AddComponent<T>();
            newObject.tag = "SDFLight";
            Selection.activeGameObject = newObject;
            Undo.RegisterCreatedObjectUndo(newObject, "Create SDFLight");
            GameObject parent = Selection.activeTransform?.gameObject;
            if (parent != null && parent != newObject)
            {
                newObject.transform.SetParent(parent.transform);
            }
        }

        
    } 
}


