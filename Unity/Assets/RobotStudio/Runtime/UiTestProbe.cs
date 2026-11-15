using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace AshinaStudio
{
    // Explicit local test switch; production sessions do not create files or record input.
    public sealed class UiTestProbe : MonoBehaviour
    {
        public StudioApp App;
        public string Output;
        int frames;float started;
        float next;

        void Update()
        {
            if(frames==0)started=Time.realtimeSinceStartup;frames++;
            if (Time.realtimeSinceStartup < next || App.Project == null) return;
            next = Time.realtimeSinceStartup + .25f;
            try
            {
                var snapshot = new
                {
                    version = Application.version, frames, elapsed=Time.realtimeSinceStartup-started, fps=frames/Math.Max(.001,Time.realtimeSinceStartup-started), pieces=App.Pieces.Active.Count, stagedFuel=App.Field?.StagedFuel.Count, blueFuel=App.BlueFuel, redFuel=App.RedFuel, shots=App.FuelShots, inventory=App.Project.inventory, network=new{connected=App.Nt?.Connected??false,status=App.Nt?.Status,topics=App.Nt?.Topics.Count??0,poseTopic=App.EffectivePoseTopic}, gizmoDrags = App.GizmoDragStarts,
                    busy = App.Busy, dialog = FileDialogs.IsOpen, nativeStructureSize = FileDialogs.NativeStructureSize,
                    notice = App.Notice, selected = App.Selected, running = App.Running,
                    field = App.Field?.Root?.GetInstanceID(), season = App.Project.season,
                    pose = App.Pose, assets = App.Project.assets.Count, nodePositions = App.Nodes.ToDictionary(n => n.Key, n => Frc.Position(n.Value.transform.localPosition)),
                    parts = App.Project.parts.Select(p => new { p.id, p.name, p.kind, p.parent, p.position, p.rotation, p.asset }).ToArray(),
                    actions = App.Project.actions.Select(a => new { a.name, a.partId, a.key, a.type, a.from, a.to }).ToArray()
                };
                string temporary = Output + ".tmp";
                File.WriteAllText(temporary, JsonConvert.SerializeObject(snapshot));
                if (File.Exists(Output)) File.Replace(temporary, Output, null);
                else File.Move(temporary, Output);
            }
            catch (Exception error) { Debug.LogWarning("UI test snapshot: " + error.Message); }
        }
    }
}

