using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]
public class MeshGen : MonoBehaviour
{
    [System.Serializable]
    public class BoneMeshProfile
    {
        public string nameKeyword;
        public float radiusMultiplier = 1.0f;
        public AnimationCurve thicknessCurve = AnimationCurve.Linear(0, 1, 1, 1);
        public AnimationCurve chainTaperCurve = AnimationCurve.Linear(0, 1, 1, 1);
        public Vector2 crossSectionScale = Vector2.one;
        public Vector3 positionOffset = Vector3.zero;
        public bool generateJointSealer = true;
        public bool generateTipSealer = true;
    }

    [Header("Target Generation")]
   
    public Transform targetRoot;

    [Header("Mesh Settings")]
    
    public int radialSegments = 16;

    public int lengthSegmentsPerBone = 8;

    public float baseRadius = 0.1f;

    [Header("Anatomy Profiles")]
    public List<BoneMeshProfile> boneProfiles = new List<BoneMeshProfile>();

    private SkinnedMeshRenderer skinnedMeshRenderer;
    private List<Vector3> verts = new List<Vector3>();
    private List<int> tris = new List<int>();
    
    private List<BoneWeight> boneWeights = new List<BoneWeight>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Transform> mappedBones = new List<Transform>();
    private Dictionary<Transform, int> boneIndexMap = new Dictionary<Transform, int>();

    private Dictionary<Transform, float> chainTaperMap = new Dictionary<Transform, float>();
    private HashSet<Transform> joints = new HashSet<Transform>();

    private BoneMeshProfile defaultProfile = new BoneMeshProfile();

    void Awake()
    {
        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        InitProfiles();
    }

    [ContextMenu("Regenerate mesh")]
    //Call to regenerate the mesh with the current settings
    public void Regen()
    {
        if (targetRoot == null)
        {
            FindRoot();
        }

        if (targetRoot == null)
        {
            Debug.LogWarning("Cannot generate could not find root bone", this);
            return;
        }
       

        GenMesh(targetRoot);
    }

    //Searches for the root bone called head but will auto default to the child  if it cant
    public void FindRoot()
    {
        Transform headBone = ChildFinder(transform, "Head");
        if (headBone != null)
        {
            targetRoot = headBone;
            Debug.Log($"assigned {headBone.name} as target skeleton root", this);
        }
        else if (transform.childCount > 0)
        {
            targetRoot = transform.GetChild(0);
            Debug.Log($"assigned {targetRoot.name} as target skeleton root", this);
        }
    }

    //Will check every child until it finds the right bone
    private Transform ChildFinder(Transform parent, string keyword)
    {
        foreach (Transform child in parent)
        {
            if (child.name.ToLower().Contains(keyword.ToLower()))
                return child;
            
            Transform found = ChildFinder(child, keyword);
            if (found != null) return found;
        }
        return null;
    }
    
    //Default profiles but can be over ridden in the inspector and by the ecological input algorithm
    private void InitProfiles()
    {
        if (boneProfiles.Count == 0)
        {
            AnimationCurve tailTaper = new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0.1f));
            AnimationCurve neckTaper = new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0.6f));

            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Head", radiusMultiplier = 1.5f, crossSectionScale = new Vector2(1f, 1.2f) });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Neck_Seg", radiusMultiplier = 0.9f, crossSectionScale = new Vector2(0.8f, 1.1f), chainTaperCurve = neckTaper });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Chest_Seg", radiusMultiplier = 2.0f, crossSectionScale = new Vector2(0.9f, 1.5f) });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Spine_Seg", radiusMultiplier = 1.2f, crossSectionScale = new Vector2(0.9f, 1.3f) });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Tail_Seg", radiusMultiplier = 0.6f, crossSectionScale = new Vector2(0.7f, 0.9f), chainTaperCurve = tailTaper, generateJointSealer = false });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Shoulder_Lateral", radiusMultiplier = 1.0f, crossSectionScale = new Vector2(1.1f, 1.0f) });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Pelvis_Lateral", radiusMultiplier = 1.1f, crossSectionScale = new Vector2(1.2f, 1.0f) });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Front_Hip", radiusMultiplier = 0.8f, crossSectionScale = new Vector2(0.8f, 0.9f) });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Front_Knee", radiusMultiplier = 0.6f, crossSectionScale = new Vector2(0.7f, 0.8f) });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Front_Ankle", radiusMultiplier = 0.4f, crossSectionScale = new Vector2(0.8f, 0.6f) });
            
           
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Front_ToeBase", radiusMultiplier = 0.3f, crossSectionScale = new Vector2(1.2f, 0.5f), generateTipSealer = false });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Rear_Hip", radiusMultiplier = 1.0f, crossSectionScale = new Vector2(0.9f, 1.1f) });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Rear_Knee", radiusMultiplier = 0.7f, crossSectionScale = new Vector2(0.8f, 0.9f) });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Rear_Ankle", radiusMultiplier = 0.5f, crossSectionScale = new Vector2(0.8f, 0.7f) });
            boneProfiles.Add(new BoneMeshProfile { nameKeyword = "Rear_ToeBase", radiusMultiplier = 0.35f, crossSectionScale = new Vector2(1.2f, 0.5f), generateTipSealer = false });
            
            
            
        }
    }

    //Called to creat the mesh

    public void GenMesh(Transform rootBone = null)
    {
        if (rootBone == null)
        {
            Debug.LogWarning("No root bone provided searching for another", this);
            Regen();
            return;
        }

        if (skinnedMeshRenderer == null) skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        if (boneProfiles == null || boneProfiles.Count == 0) InitProfiles();
        DestroyImmediate(skinnedMeshRenderer.sharedMesh);

        //Preps to set the mesh by clearning the data from the previous iteration

        verts.Clear();
        tris.Clear();
        boneWeights.Clear();
        uvs.Clear();
        mappedBones.Clear();
        boneIndexMap.Clear();
        
        chainTaperMap.Clear();
        joints.Clear();

        SetChainTaper(rootBone, null, 0, 0);

        TraverseSkeleton(rootBone);


        Mesh proceduralMesh = new Mesh();
        proceduralMesh.name = $"ProceduralSkinnedMesh_{System.DateTime.Now.Second}";
        
        if (verts.Count >= 65000) proceduralMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        proceduralMesh.vertices = verts.ToArray();
        proceduralMesh.triangles = tris.ToArray();
        proceduralMesh.uv = uvs.ToArray();
        proceduralMesh.boneWeights = boneWeights.ToArray();

        Matrix4x4[] bindposes = new Matrix4x4[mappedBones.Count];
        for (int i = 0; i < mappedBones.Count; i++)
        {
            bindposes[i] = mappedBones[i].worldToLocalMatrix * transform.localToWorldMatrix;
        }
        proceduralMesh.bindposes = bindposes;

        proceduralMesh.RecalculateNormals();
        proceduralMesh.RecalculateBounds();

       

        proceduralMesh.RecalculateTangents();

        skinnedMeshRenderer.bones = mappedBones.ToArray();
        skinnedMeshRenderer.rootBone = rootBone;
        skinnedMeshRenderer.sharedMesh = proceduralMesh;
        
        if (skinnedMeshRenderer.sharedMaterial == null)
        {
            skinnedMeshRenderer.sharedMaterial = new Material(Shader.Find("Standard"));
        }

        
        Debug.Log($"Vertecies: {proceduralMesh.vertexCount} | Triangles: {proceduralMesh.triangles.Length/3} | Bones: {mappedBones.Count}", this);
    }

    //Chain length is important for calculating taper because otherwise there is no reference point for the end
    private int GetChainLen(Transform bone, BoneMeshProfile profile)
    {
        int maxLen = 0;
        foreach (Transform child in bone)
        {
            if (GetProfiles(child) == profile)
            {
                maxLen = Mathf.Max(maxLen, GetChainLen(child, profile));
            }
        }
        return 1 + maxLen;
    }

    //Chain taper is used to calcaulte the taper of the entire mesh bone by bone
    private void SetChainTaper(Transform bone, BoneMeshProfile currentProfile, int currentDepth, int totalLength)
    {
        BoneMeshProfile profile = GetProfiles(bone);

        if (profile != currentProfile)
        {
            currentProfile = profile;
            currentDepth = 0;
            totalLength = GetChainLen(bone, profile);
        }

        float t = totalLength > 1 ? (float)currentDepth / (totalLength - 1) : 0f;
        chainTaperMap[bone] = t;

        foreach (Transform child in bone)
        {
            int nextDepth = (GetProfiles(child) == currentProfile) ? currentDepth + 1 : currentDepth;
            SetChainTaper(child, currentProfile, nextDepth, totalLength);
        }
    }

    //Moves along the skeleton and creates a cylinder for each bone
    private void TraverseSkeleton(Transform currentBone)
    {
        foreach (Transform child in currentBone)
        {
            CylinderBuilder(currentBone, child);
            TraverseSkeleton(child);
        }
    }

    private int BoneIndexer(Transform bone)
    {
        if (boneIndexMap.TryGetValue(bone, out int index)) return index;
        
        index = mappedBones.Count;
        mappedBones.Add(bone);
        boneIndexMap.Add(bone, index);
        return index;
    }

    private Vector3 GetJointBisector(Transform bone, Vector3 defaultDir)
    {
        if (bone.parent == null || bone.childCount == 0) return defaultDir;

        Vector3 inDir = (bone.position - bone.parent.position).normalized;
        
        Vector3 bestOutDir = defaultDir;
        float maxDot = -2f;
        
        foreach (Transform child in bone)
        {
            Vector3 outDir = (child.position - bone.position).normalized;
            float dot = Vector3.Dot(inDir, outDir);
            if (dot > maxDot)
            {
                maxDot = dot;
                bestOutDir = outDir;
            }
        }

        Vector3 bisector = (inDir + bestOutDir).normalized;
        
        
        
        return bisector;
    }

    //Creates the cylinder between bones based on the mesh profiles
    private void CylinderBuilder(Transform startBone, Transform endBone)
    {
        BoneMeshProfile startProfile = GetProfiles(startBone);
        BoneMeshProfile endProfile = GetProfiles(endBone);

        Vector3 startWorld = startBone.TransformPoint(startProfile.positionOffset);
        Vector3 endWorld = endBone.TransformPoint(endProfile.positionOffset);
        
        Vector3 boneDirection = endWorld - startWorld;
        float length = boneDirection.magnitude;
        
        if (length < 0.001f) return;
        Vector3 currentDirNorm = boneDirection.normalized;

        Vector3 startDirFlush = GetJointBisector(startBone, currentDirNorm);
        Vector3 endDirFlush = GetJointBisector(endBone, currentDirNorm);

        Vector3 upStart = startBone.up;
        Vector3 upEnd = endBone.up;

        if (Mathf.Abs(Vector3.Dot(startDirFlush, upStart)) > 0.99f) upStart = startBone.forward;
        if (Mathf.Abs(Vector3.Dot(endDirFlush, upEnd)) > 0.99f) upEnd = endBone.forward;

        Quaternion rotStart = Quaternion.LookRotation(startDirFlush, upStart);
        Quaternion rotEnd = Quaternion.LookRotation(endDirFlush, upEnd);

        int startIndex = verts.Count;
        int startBoneIndex = BoneIndexer(startBone);
        int endBoneIndex = BoneIndexer(endBone);

        float startChainTaper = chainTaperMap.ContainsKey(startBone) ? startProfile.chainTaperCurve.Evaluate(chainTaperMap[startBone]) : 1f;
        float endChainTaper = chainTaperMap.ContainsKey(endBone) ? endProfile.chainTaperCurve.Evaluate(chainTaperMap[endBone]) : 1f;

        for (int h = 0; h <= lengthSegmentsPerBone; h++)
        {
            float t = (float)h / lengthSegmentsPerBone;
            Vector3 centerSampleWorld = Vector3.Lerp(startWorld, endWorld, t);
            Quaternion currentRotation = Quaternion.Slerp(rotStart, rotEnd, t);

            float blendedRadiusMultiplier = Mathf.Lerp(startProfile.radiusMultiplier, endProfile.radiusMultiplier, t);
            Vector2 blendedCrossSection = Vector2.Lerp(startProfile.crossSectionScale, endProfile.crossSectionScale, t);
            
            float curveModifier = startProfile.thicknessCurve.Evaluate(t);
            float blendedChainTaper = Mathf.Lerp(startChainTaper, endChainTaper, t);
            
            float finalRadius = Mathf.Max(baseRadius * blendedRadiusMultiplier * curveModifier * blendedChainTaper, 0.001f);

            BoneWeight weight = new BoneWeight();
            weight.boneIndex0 = startBoneIndex;
            weight.weight0 = 1f - t;
            weight.boneIndex1 = endBoneIndex;
            weight.weight1 = t;      

            for (int r = 0; r < radialSegments; r++)
            {
                float angle = (float)r / radialSegments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * blendedCrossSection.x;
                float y = Mathf.Sin(angle) * blendedCrossSection.y;
                Vector3 localRingOffset = new Vector3(x, y, 0) * finalRadius;
                
                Vector3 worldVertexPos = centerSampleWorld + (currentRotation * localRingOffset);
                verts.Add(transform.InverseTransformPoint(worldVertexPos));
                boneWeights.Add(weight);
                uvs.Add(new Vector2((float)r / radialSegments, t));
            }
        }

        for (int h = 0; h < lengthSegmentsPerBone; h++)
        {
            int currentRingStart = startIndex + (h * radialSegments);
            int nextRingStart = startIndex + ((h + 1) * radialSegments);

            for (int r = 0; r < radialSegments; r++)
            {
                int nextR = (r + 1) % radialSegments;

                int v0 = currentRingStart + r;
                int v1 = currentRingStart + nextR;
                int v2 = nextRingStart + r;
                int v3 = nextRingStart + nextR;

                tris.Add(v0); tris.Add(v1); tris.Add(v3);
                tris.Add(v0); tris.Add(v3); tris.Add(v2);
            }
        }

        if (startProfile.generateJointSealer && !joints.Contains(startBone))
        {
            float rStart = Mathf.Max(baseRadius * startProfile.radiusMultiplier * startProfile.thicknessCurve.Evaluate(0f) * startChainTaper, 0.001f);
            JointGen(startBone, startWorld, rotStart, rStart, startProfile.crossSectionScale);
            joints.Add(startBone);
        }

       
        if (endProfile.generateJointSealer && endProfile.generateTipSealer && !joints.Contains(endBone) && endBone.childCount == 0)
        {
            float rEnd = Mathf.Max(baseRadius * endProfile.radiusMultiplier * endProfile.thicknessCurve.Evaluate(1f) * endChainTaper, 0.001f);
            JointGen(endBone, endWorld, rotEnd, rEnd, endProfile.crossSectionScale);
            joints.Add(endBone);
        }
    }

    //Creates a dome at the end of joints to make sure the mesh is water tight
    private void JointGen(Transform bone, Vector3 centerWorld, Quaternion rotation, float radius, Vector2 crossSectionScale)
    {
        int latSegments = lengthSegmentsPerBone;
        int lonSegments = radialSegments;
        int startIndex = verts.Count;
        
        BoneWeight weight = new BoneWeight();
        weight.boneIndex0 = BoneIndexer(bone);
        weight.weight0 = 1f;

        float zScale = (crossSectionScale.x + crossSectionScale.y) * 0.5f;

        for (int lat = 0; lat <= latSegments; lat++)
        {
            float theta = lat * Mathf.PI / latSegments;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);

            for (int lon = 0; lon <= lonSegments; lon++)
            {
                float phi = lon * 2f * Mathf.PI / lonSegments;
                
                Vector3 localPos = new Vector3(
                    Mathf.Cos(phi) * sinTheta * crossSectionScale.x,
                    Mathf.Sin(phi) * sinTheta * crossSectionScale.y,
                    cosTheta * zScale 
                ) * radius;

                Vector3 worldPos = centerWorld + (rotation * localPos);
                verts.Add(transform.InverseTransformPoint(worldPos));
                boneWeights.Add(weight);
                uvs.Add(new Vector2((float)lon / lonSegments, (float)lat / latSegments));
            }
        }

        //Creates triangles to form the dome
        for (int lat = 0; lat < latSegments; lat++)
        {
            for (int lon = 0; lon < lonSegments; lon++)
            {
                int current = lat * (lonSegments + 1) + lon;
                int next = current + lonSegments + 1;

                tris.Add(startIndex + current);
                tris.Add(startIndex + next);
                tris.Add(startIndex + current + 1);

                tris.Add(startIndex + current + 1);
                tris.Add(startIndex + next);
                tris.Add(startIndex + next + 1);
            }
        }
    }

    //Retreives bone profiles
    private BoneMeshProfile GetProfiles(Transform bone)
    {
        if (bone == null) return defaultProfile;

        string boneNameLower = bone.name.ToLower();
        
       
        foreach (var profile in boneProfiles)
        {
            if (!string.IsNullOrEmpty(profile.nameKeyword) && boneNameLower.Contains(profile.nameKeyword.ToLower()))
            {
                return profile;
            }
        }

       
        if (bone.parent != null)
        {
            return GetProfiles(bone.parent);
        }

        return defaultProfile;
    }
}