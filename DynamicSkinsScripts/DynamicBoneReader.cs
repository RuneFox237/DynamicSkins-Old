using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class DynamicBoneReader : MonoBehaviour
{
  [SerializeField]
  public DynamicBone bones;
  
  [TextArea]
  public string Strtest;

  public string CreateConstructor(DynamicBone DB)
  {
    string constructor = "ModificationName.dynamicBoneData = new DynamicBoneData(" +
      PrintString(DB.m_Root.name) + ", " +
      PrintFloat(DB.m_Damping) + ", " + PrintAnimationCurve(DB.m_DampingDistrib) + ", " +
      PrintFloat(DB.m_Elasticity) + ", " + PrintAnimationCurve(DB.m_ElasticityDistrib) + ", " +
      PrintFloat(DB.m_Stiffness) + ", " + PrintAnimationCurve(DB.m_StiffnessDistrib) + ", " +
      PrintFloat(DB.m_Inert) + ", " + PrintAnimationCurve(DB.m_InertDistrib) + ", " +
      PrintFloat(DB.m_Radius) + ", " + PrintAnimationCurve(DB.m_RadiusDistrib) + ", " +
      PrintFloat(DB.m_EndLength) + ", " + PrintVector3(DB.m_EndOffset) + "," +
      PrintVector3(DB.m_Gravity) + ", " + PrintVector3(DB.m_Force) + ", " +
      PrintColliders(DB.m_Colliders) + ", " +
      PrintExclusions(DB.m_Exclusions) + ", " +
      "DynamicBone.FreezeAxis." + DB.m_FreezeAxis.ToString() + ");";
    return constructor;
  }

  string PrintColliders(List<DynamicBoneColliderBase> colliders)
  {
    string str = "\nnew List<DynamicBoneColliderData>() {";
    foreach (var coll in colliders)
    {
      str += "\n  " + PrintCollider(coll) + ", ";
    }
    
    if (colliders.Count != 0)
    {
      str = str.Remove(str.Length - 2);
      str += "\n";
    }

    str += "}";
    return str;
  }

  string PrintCollider(DynamicBoneColliderBase collider)
  {
    //Let's see if this work
    DynamicBoneCollider coll = collider as DynamicBoneCollider;

    return "new DynamicBoneColliderData(" + 
      PrintString(coll.transform.name) + ", " +
      "DynamicBoneCollider.Direction." + coll.m_Direction + ", " +
      PrintVector3(coll.m_Center) + ", " +
      "DynamicBoneCollider.Bound." + coll.m_Bound + ", " +
      PrintFloat(coll.m_Radius) +", "+ PrintFloat(coll.m_Height) + ")";
  }

  //adds quotes around a string to turn it into a static string
  string PrintString(string str)
  { 
    return "\""+str+"\"";
  }

  string PrintFloat(float f)
  {
    return f + "f";
  }

  string PrintExclusions(List<Transform> exclusions)
  {
    string list = "new List<string>() {";
    foreach (var trans in exclusions)
    {
      list += PrintString(trans.name) + ", ";
    }

    //remove the last two characters ', ' that were placed in the last loop of forloop
    if (exclusions.Count != 0)
    {
      list = list.Remove(list.Length - 2);
    }

    list += "}";

    return list;
  }

  //Note supports bare functionality right now
  string PrintAnimationCurve(AnimationCurve curve)
  {
    if (curve == null || curve.keys.Length == 0)
      return "null";


    string animcurve = "new AnimationCurve( ";

    /////////////////////
    /// add curve keys
    foreach (var key in curve.keys)
    {
      animcurve += "new Keyframe(" + PrintFloat(key.time) + ", " + PrintFloat(key.value) + "), ";
    }

    if (curve.keys.Length != 0)
    {
      //remove the last two characters ', ' that were placed in the last loop of forloop
      animcurve = animcurve.Remove(animcurve.Length - 2);
    }
    /// add curve keys
    /////////////////////

    ///Add Wrap Mode
    animcurve += ") { postWrapMode = WrapMode." + curve.postWrapMode + ", preWrapMode = WrapMode." + curve.preWrapMode + " }";

    return animcurve;
  }

  string PrintVector3(Vector3 vec)
  {
    return "new Vector3(" + vec.x + "f, " + vec.y + "f, " + vec.z + "f)";
  }
}


class DynamicBoneData
{
  public DynamicBoneData(string root,
                 float damping, AnimationCurve damping_dist,
                 float elasticity, AnimationCurve elasticity_dist,
                 float stiffness, AnimationCurve stiffness_dist,
                 float inert, AnimationCurve inert_dist,
                 //float friction, AnimationCurve friction_dist, //NOTE: looks like ROR2 is using an older version of DynBone that doesn't have friction
                 float radius, AnimationCurve radius_dist,
                 float end_length, Vector3 end_offset,
                 Vector3 gravity, Vector3 force,
                 List<DynamicBoneColliderData> colliders,
                 List<string> exclusions,
                 DynamicBone.FreezeAxis freeze_axis)
  {
    m_Root = root;
    m_Damping = damping;
    m_DampingDistrib = damping_dist;
    m_Elasticity = elasticity;
    m_ElasticityDistrib = elasticity_dist;
    m_Stiffness = stiffness;
    m_StiffnessDistrib = stiffness_dist;
    m_Inert = inert;
    m_InertDistrib = inert_dist;
    //new_DB.m_Friction = friction;
    //new_DB.m_FrictionDistrib = friction_dist;
    m_Radius = radius;
    m_RadiusDistrib = radius_dist;
    m_EndLength = end_length;
    m_EndOffset = end_offset;
    m_Gravity = gravity;
    m_Force = force;
    m_Colliders = colliders;
    m_Exclusions = exclusions;
    m_FreezeAxis = freeze_axis;

  }

  //would include string for parent_name but all dynamicbones should be created on modification_instance

  public string m_Root;
  public float m_Damping;
  public AnimationCurve m_DampingDistrib;
  public float m_Elasticity;
  public AnimationCurve m_ElasticityDistrib;
  public float m_Stiffness;
  public AnimationCurve m_StiffnessDistrib;
  public float m_Inert;
  public AnimationCurve m_InertDistrib;
  //public float friction; public AnimationCurve friction_dist; //NOTE: looks like ROR2 is using an older version of DynBone that doesn't have friction
  public float m_Radius;
  public AnimationCurve m_RadiusDistrib;
  public float m_EndLength;
  public Vector3 m_EndOffset;
  public Vector3 m_Gravity;
  public Vector3 m_Force;
  public List<DynamicBoneColliderData> m_Colliders;
  public List<string> m_Exclusions;
  public DynamicBone.FreezeAxis m_FreezeAxis;


}

class DynamicBoneColliderData
{
  public DynamicBoneColliderData(string parent_name, DynamicBoneCollider.Direction direction, Vector3 Center, DynamicBoneCollider.Bound bound, float radius, float heaight)
  {
    m_parent_name = parent_name;
    m_Direction = direction;
    m_Center = Center;
    m_Bound = bound;
    m_Radius = radius;
    m_Height = heaight;
  }

  public string m_parent_name;
  public DynamicBoneCollider.Direction m_Direction;
  public Vector3 m_Center;
  public DynamicBoneCollider.Bound m_Bound;
  public float m_Radius;
  public float m_Height;

}

#if (UNITY_EDITOR) 
[CustomEditor(typeof(DynamicBoneReader))]
class DynamicBoneReaderEditor : Editor
{
  SerializedProperty SkirtPrefab_m;

  void OnEnable()
  {
    // Fetch the objects from the GameObject script to display in the inspector
    SkirtPrefab_m = serializedObject.FindProperty("SkirtPrefab_m");
  }

  public override void OnInspectorGUI()
  {
    DrawDefaultInspector();
    DynamicBoneReader script = (DynamicBoneReader)target;    
    if (GUILayout.Button("Print"))
    {
      script.Strtest = script.CreateConstructor(script.bones);
    }
  }
}
#endif