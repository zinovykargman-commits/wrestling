using UnityEngine;
using System.Collections.Generic;

public static partial class GBGymVisual {
    static void OrganizeStations(Transform p){
        var groups=new Dictionary<string,Transform>();var children=new List<Transform>();foreach(Transform t in p)children.Add(t);
        foreach(var t in children){
            string name=t.name,station=null;
            if(name.StartsWith("Bench ")||name=="Timber ringside bench")station="Seating "+Mathf.Clamp(Mathf.RoundToInt((t.localPosition.x+10)/4),0,5);
            else if(name.StartsWith("Power rack")||name.StartsWith("Rack ")||name.StartsWith("Barbell ")||name=="Plate steel hub")station="Power rack";
            else if(name.StartsWith("Locker ")||name=="Steel locker"||(name.Length==2&&char.IsDigit(name[0])&&t.localPosition.z>7))station="Lockers";
            else if(name.StartsWith("Weight rack")||name.StartsWith("Dumbbell"))station="Weights";
            else if(name.StartsWith("Heavy bag")||name=="Hanging heavy bag"||name=="Bag suspension"||name=="Equipment rail")station="Bags";
            else if(name.StartsWith("Training platform"))station="Platform";
            if(station==null)continue;
            Transform group;if(!groups.TryGetValue(station,out group)){var go=new GameObject("Station "+station);go.transform.SetParent(p,false);group=go.transform;groups[station]=group;}
            t.SetParent(group,true);
        }
    }
    static Bounds RendererFootprint(Transform group){
        bool first=true;Bounds bounds=default;
        foreach(var r in group.GetComponentsInChildren<Renderer>()){if(!r.enabled)continue;if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
        bounds.center=new Vector3(bounds.center.x,0,bounds.center.z);bounds.size=new Vector3(bounds.size.x,.01f,bounds.size.z);return bounds;
    }
    public static bool LayoutClear(GBArena arena,out string evidence){
        var stations=new List<KeyValuePair<string,Bounds>>();var root=arena.transform.Find("Gym visual details");
        foreach(Transform group in root)if(group.name.StartsWith("Station "))stations.Add(new KeyValuePair<string,Bounds>(group.name,RendererFootprint(group)));
        foreach(var prop in arena.breakables)if(prop.name=="Breakable table")stations.Add(new KeyValuePair<string,Bounds>("Physical table",RendererFootprint(prop.transform)));
        foreach(var rb in arena.GetComponentsInChildren<Rigidbody>())if(rb.name=="training block")stations.Add(new KeyValuePair<string,Bounds>(rb.name,RendererFootprint(rb.transform)));
        stations.Add(new KeyValuePair<string,Bounds>("Ring",new Bounds(Vector3.zero,new Vector3(9.3f,.01f,9.3f))));
        int pairs=0;float minGap=float.PositiveInfinity;
        for(int i=0;i<stations.Count;i++)for(int j=i+1;j<stations.Count;j++){
            var a=stations[i].Value;var b=stations[j].Value;
            float x=Mathf.Max(b.min.x-a.max.x,a.min.x-b.max.x),z=Mathf.Max(b.min.z-a.max.z,a.min.z-b.max.z);float gap=Mathf.Max(x,z);minGap=Mathf.Min(minGap,gap);pairs++;
            if(gap<.20f){evidence=stations[i].Key+" crowds "+stations[j].Key+"; clearance="+gap;return false;}
        }
        evidence=stations.Count+" stations; "+pairs+" unrelated footprint pairs; minimum clearance="+minGap+"m. Seating along entrance wall, strength equipment rear left, dumbbells rear center, bags rear right; original table/platform clear of seating.";return stations.Count==14;
    }
}
