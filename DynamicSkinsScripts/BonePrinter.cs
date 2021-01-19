using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class BonePrinter : MonoBehaviour
{
  [SerializeField]
  public SkinnedMeshRenderer renderer;
  [TextArea]
  public string testarea;
  // Start is called before the first frame update
  void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

  public void PrintBones()
  {
    string str = "";
    foreach (var bone in renderer.bones)
    {
      str += bone.name + "\n";
    }

    testarea = str;
  }
}

[CustomEditor(typeof(BonePrinter))]
public class BonePrinterInspector : Editor
{
  public override void OnInspectorGUI()
  {
    DrawDefaultInspector();

    BonePrinter myScript = (BonePrinter)target;
    if (GUILayout.Button("Build Object"))
    {
      myScript.PrintBones();
    }
  }
}

