﻿using BepInEx.Logging;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RuneFoxMods;
using MonoMod.RuntimeDetour;


//NameSpace and SkinName are generated from SkinDef Generator
namespace ModName
{
  public partial class SkinNamePlugin
  {
    ///////////////////////////////////////////////////////////
    /// Add Declerations of all Modifications here
    Modification ModificationName;
    ///
    ///////////////////////////////////////////////////////////

    ///////////////////////////////////////////////////////////
    /// Local Declarations
    public static SkinDef SkinDef { get; private set; }
    private static GameObject LastModelObject;

    //private static readonly Dictionary<GameObject, Modification> appliedModificatons = new Dictionary<GameObject, Modification>();
    private static List<Modification> ModificationList = new List<Modification>();//storage for modifications
    private static Dictionary<GameObject, AppliedModifications> ModifiedObjects = new Dictionary<GameObject, AppliedModifications>();

    //This uses Name of class 
    //private static SkinNamePlugin Instance { get; set; }
    //private static ManualLogSource InstanceLogger => Instance?.Logger;
    /// Local Declarations
    ///////////////////////////////////////////////////////////


    partial void BeforeAwake()
    {
      Instance = this;

      //On.RoR2.SkinDef.Apply += SkinDefApply; //Old hook for use w/ mmhook
	  new Hook(typeof(SkinDef).GetMethod(nameof(SkinDef.Apply)), (Action<Action<SkinDef, GameObject>, SkinDef, GameObject>) SkinDefApply).Apply();
    
    }

    partial void AfterAwake()
    {
      //TODO: Add AutoGenerated code here for creating DynamicBoneData for each Modification
      //////////////////////////////////////////////////
      //Should Load all modifications here
      ModificationName = new Modification("PrefabName.prefab", "ParentName", "BodyName", false, assetBundle);
	  //TODO: Add a way to load multiple Dynamic Bone Scripts per modification
      ModificationName.dynamicBoneData = new DynamicBoneData(); //get from DB reader

      //add mods to mod list
      ModificationList.Add(ModificationName);

      //Should Load all modifications here
      //////////////////////////////////////////////////

    }

    //Name for this is generated from the skinDef generator
    static partial void BodyNameSkinNameSkinAdded(SkinDef skinDef, GameObject bodyPrefab)
    {
      SkinDef = skinDef;
    }

    ////////////////////////////////////////////////////////////////////////////
    ////// Local Functions (these should not need to be changed when added to different skins)

    //private static void SkinDefApply(On.RoR2.SkinDef.orig_Apply orig, SkinDef self, GameObject modelObject) //Old SkinDefApply for use w/ mmhook
    private static void SkinDefApply(Action<SkinDef, GameObject> orig, SkinDef self, GameObject modelObject)
	{
      orig(self, modelObject);

      RemoveInvalidModelObjects();

      ModifiedObjects.TryGetValue(modelObject, out var modificatons);

      try
      {
        //if we are on another character/skin
        if (self != SkinDef)
        {
          if (modificatons != null)
          {
            ClearSkinModifications(LastModelObject, modificatons);
          }
          return;
        }

        if (modificatons == null)
        {
          //otherwise if are now applying modded skin and no modifcations have been made, then apply modifications

          //create new Applied Entry and pass into Apply
          AppliedModifications NewMods = new AppliedModifications();
          ModifiedObjects.Add(modelObject, NewMods);
          ApplySkinModifications(modelObject, NewMods);
        }
      }
      catch (Exception e)
      {
        //error logging may need to be skin specific
        InstanceLogger.LogWarning("An error occured while adding accessories to a skin");
        InstanceLogger.LogError(e);
      }

      //print heiarchy
      //RuneFoxMods.Utils.readheiarchy(modelObject);
    }

    private static void RemoveInvalidModelObjects()
    {
      foreach (var modelObject in ModifiedObjects.Keys.Where(el => !el).ToList())
      {
        ModifiedObjects.Remove(modelObject);
      }
    }

    private static void ClearSkinModifications(GameObject modelObject, AppliedModifications modifications)
    {
      //NOTE: modifications that modify the base bone list need to have their bones removed first before destruction
      //Modifications that modify the base bone list have to be removed in reverse order in order to maintain correct indexing

      //Clear Mods that modify base bone list, these need to be done in reverse order
      while (modifications.BaseModelModifications.Count != 0)
      {
        var mod = modifications.BaseModelModifications.Pop();

        clearModification(mod, modelObject, modifications);
      }

      //clear rest of mods
      while (modifications.OtherModifications.Count != 0)
      {
        clearModification(modifications.OtherModifications[0], modelObject, modifications); //clearmodification calls remove on applied Mod.
      }
	  
	  //remove the object from modified once all mods are cleared
      ModifiedObjects.Remove(modelObject);
    }

    private static void ApplySkinModifications(GameObject modelObject, AppliedModifications modifications)
    {
      var characterModel = modelObject.GetComponent<CharacterModel>();

      LastModelObject = modelObject;
      //////////////////////////////////////////////////////////////////////////////////////
      ///
      foreach (var mod in ModificationList)
      {

        ApplyModification(modelObject, characterModel, mod, modifications);
      }
      ///
      //////////////////////////////////////////////////////////////////////////////////////

    }

    ////// Local Functions
    ////////////////////////////////////////////////////////////////////////////


    /// <summary>
    /// 
    /// </summary>
    /// <param name="modelObject">GameObject of ModelObject</param>
    /// <param name="modifications">List for Storing Modifctions</param>
    /// <param name="characterModel">Character Model of ModelObject</param>
    /// <param name="modification">Modification to be apllied</param>
    private static void ApplyModification(GameObject modelObject, CharacterModel characterModel, Modification modification, AppliedModifications modifications)
    {

      //Get aramture bone that new prefab will be parented to
      var bodyname = modification.bodyname;
      var parentname = modification.parentname;

      var parentBone = RuneFoxMods.Utils.FindChildInTree(modelObject.transform, parentname);
      //var parentBone = modelObject.transform.Find(RuneFoxMods.ChildHelper.GetPath(bodyname, parentname));

      GameObject newPart;

      //we have to instantiate the prefabs in two different ways based on if they affect the model or not

      if (modification.affectsbasemodel)
      {
        //instantiate the model
        newPart = GameObject.Instantiate(modification.prefab, parentBone, false);
        newPart.name = RuneFoxMods.Utils.RemoveCloneNaming(newPart.name);
        modification.instance = newPart;
        modification.inst_armature = newPart; //the armature of the modifications that affect the base mode is the whole prefab

        ///////////////////////////////////////////////////////////
        /// Add Bones to Base Armature here
        var skinRenderers = GetBaseSkinRenderers(modelObject);

        var newBones = skinRenderers[0].bones.ToList();//assumption is that we find a skinrenderer here, if not then whoops

        var newBoneIndex = FindBoneIndex2(parentBone, newBones);
        //insert new bones into array at end of array
        var modBoneArray = BoneArrayBuilder(modification.instance.transform);
        newBones.InsertRange(newBoneIndex, modBoneArray);

        //add index and bonecount to modification
        modification.boneIndex = newBoneIndex;
        modification.boneCount = modBoneArray.Length;

        //assign bones to skin renderers
        foreach (var renderer in skinRenderers)
        {
          renderer.bones = newBones.ToArray();
        }

        modifications.BaseModelModifications.Push(modification);

        /// Add Bones to Base Armature here
        ///////////////////////////////////////////////////////////
      }
      else
      {
        //instantiate it w/ model as parent
        newPart = GameObject.Instantiate(modification.prefab, modelObject.transform, false);
        newPart.name = RuneFoxMods.Utils.RemoveCloneNaming(newPart.name);
        modification.instance = newPart;

        var armature = GetArmature(newPart);

        //if (armature == null)
        //  Debug.Log("Armature not found");
        //else
        //  Debug.Log("armature found: " + armature.name);

        //then parent the armature to the parentboned
        armature.transform.SetParent(parentBone, false);
        modification.inst_armature = armature.gameObject;
      }
      modification.instance = newPart;

	  //TODO: add a way to load multiple DynamicBone Scripts for a single modification
	
      ///////////////////////////////////////////////////////////
      /// Add dynamic bones stuff here
      /// Things like DynamicBones Component, assigning values to dynamic bones component, adding DB_Colliders and editing them, etc.

      //======================================
      //Add Dynamic Bone Component
      DynamicBone DB = modification.instance.AddComponent<DynamicBone>();
      modification.inst_dynamicBone = DB;

      //======================================
      /// Add DynamicBones Colliders to other armature bones (Need to do this before Modifying Dynamic Bones component as we add them to the DB list during modification)
      List<DynamicBoneCollider> bonelist = new List<DynamicBoneCollider>();

      foreach (var colliderData in modification.dynamicBoneData.m_Colliders)
      {
        var parent = RuneFoxMods.Utils.FindChildInTree(modelObject.transform, colliderData.m_parent_name);
        //var parent = modelObject.transform.Find(RuneFoxMods.ChildHelper.GetPath(bodyname, colliderData.m_parent_name));
        
		var bonecollider = parent.gameObject.AddComponent<DynamicBoneCollider>();

        bonecollider.m_Direction = colliderData.m_Direction;
        bonecollider.m_Center = colliderData.m_Center;
        bonecollider.m_Bound = colliderData.m_Bound;
        bonecollider.m_Radius = colliderData.m_Radius;
        bonecollider.m_Height = colliderData.m_Height;

        bonelist.Add(bonecollider);
      }

      modification.inst_DB_colliders = bonelist;

      //======================================
      // Modify DynamicBones Component with data

      var root = RuneFoxMods.Utils.FindChildInTree(modification.inst_armature.transform, modification.dynamicBoneData.m_Root);

      DB.m_Root = root;
      DB.m_Damping = modification.dynamicBoneData.m_Damping;
      DB.m_DampingDistrib = modification.dynamicBoneData.m_DampingDistrib;
      DB.m_Elasticity = modification.dynamicBoneData.m_Elasticity;
      DB.m_ElasticityDistrib = modification.dynamicBoneData.m_ElasticityDistrib;
      DB.m_Stiffness = modification.dynamicBoneData.m_Stiffness;
      DB.m_StiffnessDistrib = modification.dynamicBoneData.m_StiffnessDistrib;
      DB.m_Inert = modification.dynamicBoneData.m_Inert;
      DB.m_InertDistrib = modification.dynamicBoneData.m_InertDistrib;
      DB.m_Radius = modification.dynamicBoneData.m_Radius;
      DB.m_RadiusDistrib = modification.dynamicBoneData.m_RadiusDistrib;
      DB.m_EndLength = modification.dynamicBoneData.m_EndLength;
      DB.m_EndOffset = modification.dynamicBoneData.m_EndOffset;
      DB.m_Gravity = modification.dynamicBoneData.m_Gravity;
      DB.m_Force = modification.dynamicBoneData.m_Force;

      DB.m_Colliders = bonelist;
      DB.m_Exclusions = new List<Transform>();
      foreach (var exclude in modification.dynamicBoneData.m_Exclusions)
      {
        //NOTE: Assumption here is that the dynamic bone root is part of the new armature and we are only excluding bones located in root
        var transform = RuneFoxMods.Utils.FindChildInTree(root, exclude);

        if (transform != null)
          DB.m_Exclusions.Add(transform);
        else
          Debug.Log("Tried to exclude a transform that could not be found");
      }


      DB.m_FreezeAxis = modification.dynamicBoneData.m_FreezeAxis;

      //TODO: Read DB and compare it to what's made in OG mod cause skirt is behaving oddly

      /// Add dynamic bones stuff here
      ///////////////////////////////////////////////////////////

      ///////////////////////////////////////////////////////////
      /// Add renderers to the character's renderer list

      //get renderers
      var renderers = newPart.GetComponentsInChildren<SkinnedMeshRenderer>(true);

      //resize render array to account for new renderers
      Array.Resize(ref characterModel.baseRendererInfos, characterModel.baseRendererInfos.Length + renderers.Length);

      //NOTE: Need to save the number of renderers added to the character render info so we can remove them cleanly. Probably add this to modifications
      if (renderers.Length != 0)
      {
        int i = renderers.Length;
        foreach (var renderer in renderers)
        {
          //2 to add - 3 in
          //resize array to 5
          //first is added at 5-2 (3) which is position 4
          //second is added at 5-1 (4) which is position 5
          //exits

          characterModel.baseRendererInfos[characterModel.baseRendererInfos.Length - i] = new CharacterModel.RendererInfo
          {
            renderer = renderers[renderers.Length - i],
            ignoreOverlays = false,
            defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
            defaultMaterial = renderer.sharedMaterial
          };

          i--; //decrement i to reach the next renederer
        }
      }
      /// Add renderers to the character's renderer list
      ///////////////////////////////////////////////////////////

      //Push to applied modifications when done
      modifications.OtherModifications.Add(modification);
    }

    private static void clearModification(Modification modification, GameObject modelObject, AppliedModifications modifications)
    {
      //Destroy Dynamic Bones colliders
      foreach (var collider in modification.inst_DB_colliders)
      {
        Destroy(collider);
      }


      //Remove Additions to Bone Arrays
      if (modification.affectsbasemodel)
      {
        var renderers = GetBaseSkinRenderers(modelObject);
        var oldBones = renderers[0].bones.ToList();

        oldBones.RemoveRange(modification.boneIndex, modification.boneCount);

        foreach (var renderer in renderers)
        {
          renderer.bones = oldBones.ToArray();
        }
      }

      //Destroy Dynamic Bones Component (Probably don't have to do this since it will be destroyed along with PrefabInstance if parented to it)
      Destroy(modifications.OtherModifications[0].inst_dynamicBone);
      //Destroy Armature
      Destroy(modifications.OtherModifications[0].inst_armature);
      //Destroy Prefab Instance
      Destroy(modifications.OtherModifications[0].instance);

      bool removed = modifications.OtherModifications.Remove(modification);
      if (!removed) InstanceLogger.LogError("Skin Modification was not removed");
    }

    ////////////////////////////////////////////////////////////////////////////
    ////// Local Classes

    class Modification
    {

      public Modification(string PrefabPath, string ParentName, string BodyName, bool AffectsBaseModel, AssetBundle assetBundle)
      {
        bodyname = BodyName;
        prefabpath = PrefabPath;
        parentname = ParentName;
        affectsbasemodel = AffectsBaseModel;
        prefab = assetBundle.LoadAsset<GameObject>(@prefabpath);
        if (prefab == null) { Debug.Log("Asset at " + PrefabPath + " was not loaded"); } //DEBUG check for if asset was not loaded
      }

      //////////////////////////////////////////////////////
      /// These are created when the Modification is created
      public string prefabpath;
      public string bodyname; //Name of the base BodyName. i.e. MercBody or MageBody
      public string parentname; //the name of the bone we want to parent this modification to
      public GameObject prefab;
      public bool affectsbasemodel; //if the modification affects the base model then we need to do additional steps
	  //TODO: Add Support for multiple Dynamic Bone Scripts per modification
	  public DynamicBoneData dynamicBoneData;

                                    /// 
      //////////////////////////////////////////////////////

      //////////////////////////////////////////////////////
      /// Used for bones that need to be added to base model
      public int boneIndex; //index of bone in bone array, created on modification
      public int boneCount; //number of bones in prefab bone armature
                            ///
      //////////////////////////////////////////////////////

      //////////////////////////////////////////////////////
      /// These objects are instanceated and destroyed on skinDefApply
      public GameObject instance; //The created instance of the prefab attatched to the character
      public GameObject inst_armature; //the armature of the created instance
	  
	  //TODO: add support for multiple DynamicBone Scripts per modification
      public DynamicBone inst_dynamicBone; //the dynamic bone attatched to the instance
      public List<DynamicBoneCollider> inst_DB_colliders = new List<DynamicBoneCollider>(); //List of Dynamic Bone Colliders that were attatched to other bones for this modification
                                                                                         ///
      //////////////////////////////////////////////////////

      //This only contained the Skinned Mesh Renderers and I think I can do these inline instead
      // //////////////////////////////////////////////////////
      // /// These don't seem to be created or destroyed and are just assigned to
      // 
      // //Note: it looks like all the mesh renderers are using the same bone list as the base code only ever looked at the one and assigned to both
      // //these were taken from the meshes at root of the model
      // SkinnedMeshRenderer[] meshRenderers;
      // 
      // /// These don't seem to be created or destroyed and are just assigned to
      // //////////////////////////////////////////////////////

    }

    class AppliedModifications
    {
      public Stack<Modification> BaseModelModifications = new Stack<Modification>();
      public List<Modification> OtherModifications = new List<Modification>();//storage for modifications
    }


    //Data classes include strings in place of transforms as we need to search for the transforms when we load the data in
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

    ////// Local Classes
    ////////////////////////////////////////////////////////////////////////////


    ////////////////////////////////////////////////////////////////////////////
    ////// Utility Funcs

    //The targetBone has to be located within the armature used by the bone list in order to find the index
    //Bones are in a different order than in prefab. Things like Clavicle.l, Neck, Clavicle.R where the order in transform is clavicle.l, clavical.r, neck
    //!!!THIS DOESN"T WORK!!!! - Use FindBoneIndex2, I think that one works as intended
    static int FindBoneIndex(Transform targetBone, List<Transform> BoneList)
    {
      /*
       * idea: find parent bone we want to add bones to
       * go up one to find it's parent
       * find the next bone after the bone we want to parent to in it's parent's heiarchy
       * find that bone's location in the bone array
       * go one step back and we should be at the place to insert
       * 
       * edge cases:
       * attatching to Root, just go to end of list
       * if parent bone is last bone in it's parent we have to go one step up, repeat until we hit root or a parent that isn't the last node in it's parent's list
       * NOTE: this will probably break on bones that have more than just bones in their child heiarchy.
       */

      if (targetBone.parent.name.Contains("Armature") == true)//if this was ROOT, ROOT is always a child of Armature on base characters
      {
        return BoneList.Count; //return the end of the list as adding a bone there will make it a child of the root
      }
      var parent = targetBone.parent;
      var index = targetBone.GetSiblingIndex();
      var childcount = parent.childCount;

      if (index == childcount - 1)//if current bone is at end of list of children of parent bone we need to go up to the next parent
      {
        return FindBoneIndex(targetBone.parent, BoneList);
      }
      else
      {
        //otherwise we can find the index of next bone by searching the bonelist for it's name
        var nextBone = targetBone.parent.GetChild(index + 1);

        //NOTE: if this is gonna break due to non_bone children it'll probably be here
        var nextBoneIndex = BoneList.FindIndex(x => x == nextBone); //will return -1 if nextbone cannot be found

        if (nextBoneIndex == -1)
        {
          //couldn't find the index of the next sibling in bone index so we look for the next-next sibling using the next sibling
          FindBoneIndex(nextBone, BoneList);
        }

        return nextBoneIndex;
      }
    }

    static int FindBoneIndex2(Transform targetBone, List<Transform> BoneList)
    {
      /*Dumb idea #2
       * We find and mark all bones in bonelist that have target as a 'root' parent then find the one with the lowest index after the target and set index to that + 1
       */

      //bool is true if transform has target as parent somehwere in it's heiarchy
      Dictionary<Transform, bool> BoneDictionary = new Dictionary<Transform, bool>();

      int targetIndex = 99999;//picked an arbitrarily large number. if there's somehow this many bones, we got bigger problems

      //could use a foreach here but for feels cleaner to interpret
      for (int i = 0; i < BoneList.Count; i++)
      {
        var bone = BoneList[i];
        if (bone.name == "ROOT") //top most bone, can't search for parent here so targetbone can't be parent, This should also be the top most bone in bonelist but who knows *shrug*
        {
          //DEBUG          Debug.Log("Root added to dict");
          //add root to bonedictionary
          BoneDictionary.Add(bone, false);
          continue;
        }

        //There are three cases
        //bone is target
        //bone is child of target
        //bone is not child of target

        if (bone == targetBone)
        {
          targetIndex = i;
        }

        var parent = bone.transform.parent;

        //check if parent is target
        if (parent == targetBone)
        {
          //DEBUG          Debug.Log("target added to dict");
          //add bone to dictionary
          BoneDictionary.Add(bone, true);
          continue;
        }

        //check if parent is in dictionary
        bool parentInDictionary = BoneDictionary.ContainsKey(parent);

        if (parentInDictionary)
        {
          //check if parent is a child of target, children of a bone that is already a child of target will also be children of target
          if (BoneDictionary[parent] == true)//parent is a child of target
          {
            //add bone to dictionary
            //DEBUG            Debug.Log("child added to dict");
            BoneDictionary.Add(bone, true);
          }
          else
          {
            //parent not a child of target, therefore children cannot be child of target
            //add bone to dictionary
            //DEBUG            Debug.Log("Non-Child added to dict");
            BoneDictionary.Add(bone, false);

            //check if index is higher than target index. this means we are either in the children of the target, or we are in the next bone after the target, which is what we want
            if (i > targetIndex)
            {
              //DEBUG              Debug.Log(highestIndex + "  " + bone.name);
              return i;//looking for lowest index higher than index of target since that is the index of the end of all the children of target
            }
          }
        }
        else //parent is not in dictionary
        {
          //Not sure what to do here, theoretically all bones should have their parent visited first before they are visited
          //DEBUG          Debug.Log("A bone was found whose parent was not in the dictionary");
          //DEBUG          Debug.Log("Bone Name: " + bone.name);
          //DEBUG          Debug.Log("Parent Name: " + parent.name);
        }
      }

      return BoneList.Count - 1;//returns end of list as that would give index that is last child of list
    }

    static SkinnedMeshRenderer[] GetBaseSkinRenderers(GameObject modelObject)
    {
      //assumption of meshes on base characters is that they are put as direct children to ModelObject
      var renderers = modelObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
      
      List<SkinnedMeshRenderer> baseskinrenderers = new List<SkinnedMeshRenderer>();
      foreach (var renderer in renderers)
      {
        //if renderer is attatched to a child of the modelobject then it is a base skin renderer
        if (renderer.transform.parent == modelObject.transform)
        {
          baseskinrenderers.Add(renderer);
        }
      }

      return baseskinrenderers.ToArray();
    }

    //NOTE: I have no idea how this works with long and wide armatures as the 2B ones are only 3 bones total
    //this will likely break
    static Transform[] BoneArrayBuilder(Transform NewBoneRoot)
    {
      List<Transform> NewBoneList = new List<Transform>();

      //read the new bone heiarchy to array depth first
      BoneArrayBuilderHelper(NewBoneRoot, NewBoneList);

      return NewBoneList.ToArray();
    }

    static void BoneArrayBuilderHelper(Transform parent, List<Transform> list)
    {
      if (!parent.name.EndsWith("_end"))
        list.Add(parent);

      for (int i = 0; i < parent.childCount; i++)
      {
        BoneArrayBuilderHelper(parent.GetChild(i), list);
      }
    }

    //Searches the gameobject for the first object with Armature in its name and returns it
    static Transform GetArmature(GameObject obj)
    {
      return GetArmatureHelper(obj);
    }

    static Transform GetArmatureHelper(GameObject obj)
    {
      if (obj.name.ToLower().Contains("armature"))
      {
        return obj.transform;
      }

      for (int i = 0; i < obj.transform.childCount; i++)
      {
        var armature = GetArmatureHelper(obj.transform.GetChild(i).gameObject);
        if (armature) return armature;
      }
      return null;
    }

    ////// Utility Funcs
    ////////////////////////////////////////////////////////////////////////////
  }
}