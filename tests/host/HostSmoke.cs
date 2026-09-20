using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Forms;
using PEPlugin;
using PEPlugin.SDX;
using TextureGrade;
using TextureGrade.ColorGrade;
using TextureGrade.Models;
using TextureGrade.WpfUI;
using TextureGrade.TextureIO;

// Built only into an isolated editor under artifacts; never ship this startup test plugin.
public sealed class HostSmoke : PEPluginClass
{
    private Timer timer;
    private PluginForm form;
    private string root, log;
    public HostSmoke() { m_option = new PEPluginOption(true, false, "TextureGrade verification"); }
    private static object Get(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); File.AppendAllText(log, "PASS " + message + Environment.NewLine); }
    public override void Run(IPERunArgs args)
    {
        root = AppDomain.CurrentDomain.BaseDirectory;
        // Guard against accidentally installing the test plugin in a user's editor.
        if (!File.Exists(Path.Combine(root, "texturegrade-test-host.marker"))) return;
        log = Path.Combine(root, "host-smoke.log"); File.WriteAllText(log, "Host " + args.Host.Version + Environment.NewLine);
        timer = new Timer { Interval = 1500 };
        timer.Tick += (s, e) => { timer.Stop(); try { Start(args.Host); } catch (Exception ex) { File.AppendAllText(log, "FAIL " + ex); } };
        timer.Start();
    }
    private void Start(IPEPluginHost host)
    {
        string fixture = Path.Combine(root, "fixture"); Directory.CreateDirectory(fixture);
        var builder = host.Builder.Pmx; var pmx = builder.Pmx();
        var bone = builder.Bone(); bone.Name = "root"; pmx.Bone.Add(bone);
        var material = builder.Material(); material.Name = "TextureGrade fixture"; material.Diffuse = new V4(1, 1, 1, 1); material.Tex = "source.png"; material.BothDraw = true;
        var coords = new[] { new V3(-5, 0, 0), new V3(5, 0, 0), new V3(5, 10, 0), new V3(-5, 10, 0) };
        var uv = new[] { new V2(0, 1), new V2(1, 1), new V2(1, 0), new V2(0, 0) };
        for (int i = 0; i < 4; i++)
        {
            var v = builder.Vertex(); v.Position = coords[i]; v.Normal = new V3(0, 0, -1); v.UV = uv[i]; v.Bone1 = bone; v.Weight1 = 1; pmx.Vertex.Add(v);
        }
        var f1 = builder.Face(); f1.Vertex1 = pmx.Vertex[0]; f1.Vertex2 = pmx.Vertex[2]; f1.Vertex3 = pmx.Vertex[1]; material.Faces.Add(f1);
        var f2 = builder.Face(); f2.Vertex1 = pmx.Vertex[0]; f2.Vertex2 = pmx.Vertex[3]; f2.Vertex3 = pmx.Vertex[2]; material.Faces.Add(f2);
        pmx.Material.Add(material); string path = Path.Combine(fixture, "texturegrade-fixture.pmx"); pmx.ToFile(path);
        Check(host.Connector.Form.OpenPMXFile(path), "Open isolated PMX fixture");
        form = new PluginForm(host); form.Show();
        var panel = (MainPanel)Get(form, "_panel"); var settings = (GradeSettings)Get(panel, "Settings");
        var recolor = new RecolorSettings { Mode = RecolorMode.Palette };
        var data = TextureLoader.Load(Path.Combine(fixture, "source.png"));
        recolor.Entries = PaletteAnalysis.Extract(PaletteAnalysis.Sample(data.rgba), 8).Select(c => PaletteEntry.Identity(c.Rgb)).ToList();
        recolor.Entries[0].Target = OklabColor.FromRgb(0x4455cc);
        recolor.Write(settings); Call(panel, "RefreshRecolorUi");
        ((Expander)((StackPanel)Get(panel, "_recolorRoot")).Parent).IsExpanded = true;
        Check(recolor.Entries.Count == 8, "Palette generated in real host");
        byte[] expected = (byte[])Call(panel, "ComputeGraded");
        panel.RefreshModel();
        string preview = host.Connector.Pmx.GetCurrentState().Material[0].Tex;
        Check(File.Exists(preview) && TextureLoader.Load(preview).rgba.SequenceEqual(expected), "RefreshModel pushes exact PNG into PMXEditor");
        panel.SaveNew();
        string saved = host.Connector.Pmx.GetCurrentState().Material[0].Tex;
        string abs = Path.IsPathRooted(saved) ? saved : Path.Combine(fixture, saved);
        Check(File.Exists(abs) && TextureLoader.Load(abs).rgba.SequenceEqual(expected), "SaveNew updates PMX material with exact PNG");
        File.AppendAllText(log, "READY for interactive reference / picker validation" + Environment.NewLine);
    }
}
