using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using PEPlugin;
using PEPlugin.Pmx;
using TextureGrade.Bridge;
using TextureGrade.TextureIO;

internal static partial class Program
{
    private static void TexturePathRoundTrips()
    {
        string folder = Path.Combine(output, "路径 #100% 空格"), textures = Path.Combine(folder, "贴图");
        Directory.CreateDirectory(textures);
        string original = Path.Combine(textures, "原图 #%20.png");
        TextureLoader.SavePng(original, Pixels(0x446688), 1, 1);
        string saved = TextureNaming.NewSavePath(original); TextureLoader.SavePng(saved, Pixels(0x6688aa), 1, 1);
        string relative = TextureNaming.ToMaterialTexPath(saved, folder);
        Check(relative == "贴图/原图 #%20_new.png", "Relative texture path preserves Unicode, spaces, hash and percent");
        Check(TextureNaming.ResolveMaterialTexPath(relative, folder) == saved, "Relative material path resolves exactly");
        Check(TextureNaming.ResolveMaterialTexPath(saved, null) == saved, "Legacy absolute texture path loads without a model directory");
        Check(TextureNaming.ResolveMaterialTexPath(relative, null) == null, "Unresolved relative path does not depend on process directory");
        Check(TextureNaming.ResolveMaterialTexPath("bad\0path", folder) == null, "Malformed material path does not abort material loading");
        Check(TextureNaming.ToMaterialTexPath(saved, null) == saved, "Unsaved model keeps a valid absolute path");
        Check(TextureNaming.ToMaterialTexPath(@"Z:\textures\image.png", @"C:\models") == @"Z:\textures\image.png", "Cross-drive texture path remains absolute");
        Check(TextureNaming.ToMaterialTexPath(@"\\server\share\tex\image.png", @"\\server\share\model") == "../tex/image.png", "Same UNC share uses a relative path");
        Check(TextureNaming.ToMaterialTexPath(@"\\server\other\image.png", @"\\server\share\model") == @"\\server\other\image.png", "Different UNC share stays absolute");

        var fixture = new BridgeFixture(Path.Combine(folder, "model.pmx"), original);
        var bridge = new PmxBridge(fixture.Host);
        Check(bridge.LoadCurrent().Materials[0].HasTexture, "Bridge loads original absolute texture");
        bridge.ApplySaved(0, saved);
        Check(fixture.Texture == relative, "Saving converts an original absolute texture to model-relative");
        bridge.Cleanup();
        Check(new PmxBridge(fixture.Host).LoadCurrent().Materials[0].TexAbsPath == saved && File.Exists(saved), "Saved texture survives closing and reopening plugin");

        bridge = new PmxBridge(fixture.Host); bridge.LoadCurrent();
        string preview = TextureNaming.PreviewPath(saved, 1); File.Copy(saved, preview, true);
        bridge.ApplyPreview(0, preview);
        string preview2 = TextureNaming.PreviewPath(saved, 2); File.Copy(saved, preview2, true);
        bridge.ApplyPreview(0, preview2);
        Check(!File.Exists(preview) && File.Exists(preview2), "Repeated preview releases the superseded temporary file");
        bridge.Cleanup();
        Check(fixture.Texture == relative && !File.Exists(preview2) && File.Exists(saved), "Close restores permanent texture before deleting preview");
        Check(new PmxBridge(fixture.Host).LoadCurrent().Materials[0].HasTexture, "Save then preview then reopen retains a readable texture");

        bridge = new PmxBridge(fixture.Host); bridge.LoadCurrent(); File.Copy(saved, preview, true);
        bridge.ApplyPreview(0, preview); fixture.Texture = original; bridge.Cleanup();
        Check(fixture.Texture == original && !File.Exists(preview), "Cleanup respects a material reference changed outside the plugin");

        bridge = new PmxBridge(fixture.Host); bridge.LoadCurrent(); File.Copy(saved, preview, true);
        bridge.ApplyPreview(0, preview); bridge.ApplySaved(0, saved); bridge.Cleanup();
        Check(fixture.Texture == relative && !File.Exists(preview) && File.Exists(saved), "Saving during preview retains permanent file and removes temp");

        bridge = new PmxBridge(fixture.Host); bridge.LoadCurrent();
        string relocated = Path.Combine(folder, "model moved"); Directory.CreateDirectory(relocated);
        fixture.ModelPath = Path.Combine(relocated, "model.pmx"); bridge.ApplySaved(0, saved);
        Check(fixture.Texture == "../贴图/原图 #%20_new.png", "Saving uses current PMX directory after model Save As");
        Check(new PmxBridge(fixture.Host).LoadCurrent().Materials[0].HasTexture, "Parent-relative path loads on reopening");

        bridge = new PmxBridge(fixture.Host); bridge.LoadCurrent(); File.Copy(saved, preview, true);
        bridge.ApplyPreview(0, preview); fixture.ModelPath = Path.Combine(folder, "model.pmx"); bridge.Cleanup();
        Check(fixture.Texture == relative && new PmxBridge(fixture.Host).LoadCurrent().Materials[0].HasTexture,
            "Closing after moving the model during preview restores a correctly rebased permanent path");

        bridge = new PmxBridge(fixture.Host); bridge.LoadCurrent(); File.Copy(saved, preview, true);
        bridge.ApplyPreview(0, preview); fixture.FailUpdates = true; bridge.Cleanup();
        Check(File.Exists(preview), "Failed host restoration preserves temporary file");
        fixture.FailUpdates = false;
    }

    // Exercise the production PmxBridge with real PEPlugin interfaces, without launching or modifying an editor.
    private sealed class BridgeFixture
    {
        public string ModelPath, Texture;
        public bool FailUpdates;
        public readonly IPEPluginHost Host;
        private readonly List<IPXMaterial> materials;
        public BridgeFixture(string path, string texture)
        {
            ModelPath = path; Texture = texture;
            materials = new List<IPXMaterial> { (IPXMaterial)Make(typeof(IPXMaterial)) };
            Host = (IPEPluginHost)Make(typeof(IPEPluginHost));
        }
        private object Make(Type type) => new InterfaceProxy(type, call =>
        {
            switch (call.MethodName)
            {
                case "get_FilePath": return ModelPath;
                case "get_Tex": return Texture;
                case "set_Tex": Texture = (string)call.Args[0]; return null;
                case "get_Name": return "Fixture";
                case "get_Material": return materials;
                case "Update": if (FailUpdates) throw new InvalidOperationException("Host unavailable"); break;
            }
            var resultType = ((MethodInfo)call.MethodBase).ReturnType;
            return resultType.IsInterface ? Make(resultType) : resultType.IsValueType && resultType != typeof(void) ? Activator.CreateInstance(resultType) : null;
        }).GetTransparentProxy();
    }

    private sealed class InterfaceProxy : RealProxy
    {
        private readonly Func<IMethodCallMessage, object> invoke;
        public InterfaceProxy(Type type, Func<IMethodCallMessage, object> invoke) : base(type) { this.invoke = invoke; }
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            try { return new ReturnMessage(invoke(call), null, 0, call.LogicalCallContext, call); }
            catch (Exception ex) { return new ReturnMessage(ex, call); }
        }
    }
}
