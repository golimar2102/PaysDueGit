using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PixelationRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class PixelSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        
        [Range(1, 8)]
        [Tooltip("Пикселизация: 1 - выкл, 2 - классика (разрешение / 2), 4 - сильный пиксель-арт")]
        public int downsampleFactor = 2;
    }

    public PixelSettings settings = new PixelSettings();
    private PixelationPass customPass;

    public override void Create()
    {
        if (customPass == null) customPass = new PixelationPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Не тратим ресурсы, если пикселизация выключена
        if (settings.downsampleFactor <= 1) return; 
        
        customPass.Setup(settings);
        renderer.EnqueuePass(customPass);
    }

    protected override void Dispose(bool disposing)
    {
        customPass?.Dispose();
    }
}

public class PixelationPass : ScriptableRenderPass
{
    private PixelationRenderFeature.PixelSettings settings;

    public void Setup(PixelationRenderFeature.PixelSettings settings)
    {
        this.settings = settings;
        this.renderPassEvent = settings.renderPassEvent;
    }

    public void Dispose() { }

    private class PassData
    {
        public TextureHandle source;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        TextureHandle source = resourceData.activeColorTexture;
        if (!source.IsValid()) return;

        // Рассчитываем пониженное пиксельное разрешение
        RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
        desc.width = Mathf.Max(1, desc.width / settings.downsampleFactor);
        desc.height = Mathf.Max(1, desc.height / settings.downsampleFactor);
        desc.depthBufferBits = 0;

        // Создаем временную текстуру. КРИТИЧЕСКИ ВАЖНО: FilterMode.Point дает жесткие пиксели без размытия
        TextureHandle tempTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "TempPixelTexture", false, FilterMode.Point);

        // ПРОХОД 1: Сжимаем исходный экран во временную маленькую текстуру
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixel Downsample", out var passData))
        {
            passData.source = source;
            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(tempTex, 0); 
            
            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
            });
        }

        // ПРОХОД 2: Растягиваем пиксели обратно на весь экран (Point-фильтр сохранит квадраты)
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixel Stretch", out var passData))
        {
            passData.source = tempTex;
            builder.UseTexture(tempTex, AccessFlags.Read);
            builder.SetRenderAttachment(source, 0); 
            
            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
            });
        }
    }
}