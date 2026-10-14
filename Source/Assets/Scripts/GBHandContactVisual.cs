using System.Collections.Generic;
using UnityEngine;

// A small visual fit for an already-held opponent. Physics, grip acquisition,
// hand aim, forces, joints and network snapshots are never changed here.
public sealed class GBHandContactVisual:MonoBehaviour {
    public const float MaximumCorrection=.028f;
    const float NearbySurface=.115f;
    readonly Transform[] shownHands=new Transform[2];
    readonly Vector3[][] palmSamples=new Vector3[2][];
    readonly Vector3[] offsets=new Vector3[2];
    readonly GBTarget[] previousTargets=new GBTarget[2];
    readonly float[] gaps=new float[2];
    readonly bool[] eligible=new bool[2];
    GBWrestler owner;
    public Transform ShownHand(int hand){return shownHands[hand];}
    public Vector3 Correction(int hand){return offsets[hand];}
    public float UncorrectedContactGap(int hand){return gaps[hand];}
    public bool ContactEligible(int hand){return eligible[hand];}
    public int PalmSampleCount(int hand){return palmSamples[hand]==null?0:palmSamples[hand].Length;}
    public float CorrectedContactGap(int hand){
        var target=owner.hands[hand].target;if(!target||palmSamples[hand].Length==0)return 0;
        Vector3 contact=target.transform.TransformPoint(owner.hands[hand].localAnchor);float best=float.PositiveInfinity;
        var shownMatrix=shownHands[hand].localToWorldMatrix;
        foreach(var p in palmSamples[hand])best=Mathf.Min(best,(shownMatrix.MultiplyPoint3x4(p)-contact).sqrMagnitude);
        return Mathf.Sqrt(best);
    }

    public void Initialize(GBWrestler wrestler,Transform[] bones,SkinnedMeshRenderer[] skins){
        owner=wrestler;
        for(int hand=0;hand<2;hand++){
            int index=hand==0?5:8;
            var proxy=new GameObject(hand==0?"Left render-only hand contact":"Right render-only hand contact");
            proxy.transform.SetParent(transform,false);shownHands[hand]=proxy.transform;
            shownHands[hand].SetPositionAndRotation(owner.bodies[index].transform.position,owner.bodies[index].transform.rotation);
            var points=new List<Vector3>();
            // Use actual authored palm/finger surface vertices, not an invented
            // palm location. Partial forearm weights keep the wrist continuous.
            foreach(var skin in skins){
                if(!skin.name.EndsWith(" athlete"))continue;
                var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                var bind=mesh.bindposes[index];
                for(int v=0;v<vertices.Length;v++){
                    var w=weights[v];
                    float amount=(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
                    if(amount<.85f)continue;
                    Vector3 local=bind.MultiplyPoint3x4(vertices[v]);
                    if(local.y>=-.15f&&local.y<=.045f)points.Add(local);
                }
            }
            int count=Mathf.Min(128,points.Count);
            palmSamples[hand]=new Vector3[count];
            for(int p=0;p<count;p++)palmSamples[hand][p]=points[p*points.Count/count];
            bones[index]=shownHands[hand];
        }
        foreach(var skin in skins)skin.bones=bones;
        RenderFrame(0);
    }

    public void RenderFrame(float dt){
        if(owner==null)return;
        for(int hand=0;hand<2;hand++){
            int index=hand==0?5:8;var real=owner.bodies[index].transform;
            var hold=owner.hands[hand];var target=hold.target;
            eligible[hand]=false;gaps[hand]=0;
            Vector3 desired=Vector3.zero;
            // Clients have the authoritative target/anchor but no local grip
            // joint. The same existing held-target state is sufficient on both.
            if(target&&target.wrestler&&target.wrestler!=owner&&target.grabbable&&target.gameObject.activeInHierarchy&&palmSamples[hand].Length>0){
                var shape=target.GetComponent<Collider>();
                Vector3 contact=target.transform.TransformPoint(hold.localAnchor);
                // A stretched but already real hold can lie beyond the mouse
                // motor's nominal reach. Actual skin proximity below is the
                // bound; the visual pass grants no hold or physical reach.
                if(shape&&shape.enabled){
                    // Sample one unchanged interpolated transform for the whole
                    // palm, avoiding a native transform lookup for every vertex.
                    var handMatrix=real.localToWorldMatrix;
                    Vector3 nearest=handMatrix.MultiplyPoint3x4(palmSamples[hand][0]);
                    float best=(nearest-contact).sqrMagnitude;
                    for(int p=1;p<palmSamples[hand].Length;p++){
                        Vector3 point=handMatrix.MultiplyPoint3x4(palmSamples[hand][p]);
                        float distance=(point-contact).sqrMagnitude;
                        if(distance<best){best=distance;nearest=point;}
                    }
                    gaps[hand]=Mathf.Sqrt(best);
                    if(gaps[hand]<NearbySurface){
                        eligible[hand]=true;
                        Vector3 towards=contact-nearest;
                        // Keep a tiny surface gap instead of forcing fingers
                        // through a coarse collider or concealing a distant grip.
                        desired=towards.sqrMagnitude>.000016f?towards.normalized*Mathf.Min(MaximumCorrection,towards.magnitude-.004f):Vector3.zero;
                    }
                }
            }
            if(!eligible[hand])offsets[hand]=Vector3.zero;
            else{
                if(previousTargets[hand]!=target)offsets[hand]=Vector3.zero;
                offsets[hand]=Vector3.Lerp(offsets[hand],desired,1-Mathf.Exp(-22*Mathf.Max(0,dt)));
                offsets[hand]=Vector3.ClampMagnitude(offsets[hand],MaximumCorrection);
            }
            previousTargets[hand]=eligible[hand]?target:null;
            shownHands[hand].SetPositionAndRotation(real.position+offsets[hand],real.rotation);
        }
    }
}
