﻿using Herno.Renderer;
using Herno.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veldrid;
using Veldrid.SPIRV;
using System.Numerics;
using ImGuiNET;

namespace Herno
{
  public class TestCanvas : UICanvas
  {
    struct VertexPositionColor
    {
      public Vector2 Position; // This is the position, in normalized device coordinates.
      public RgbaFloat Color; // This is the color of the vertex.
      public VertexPositionColor(Vector2 position, RgbaFloat color)
      {
        Position = position;
        Color = color;
      }
      public const uint SizeInBytes = 24;
    }

    DeviceBuffer VertexBuffer { get; set; }
    DeviceBuffer IndexBuffer { get; set; }
    BufferList<VertexPositionColor> Buffer { get; }
    DeviceBuffer ProjMatrix { get; set; }
    ResourceLayout Layout { get; set; }
    ResourceSet MainResourceSet { get; set; }
    Shader[] Shaders { get; set; }
    Pipeline Pipeline { get; set; }

    const string VertexCode = @"
      #version 450

      layout(location = 0) in vec2 Position;
      layout(location = 1) in vec4 Color;

      layout (binding = 0) uniform ProjectionMatrixBuffer
      {
          mat4 projection_matrix;
      };

      layout(location = 0) out vec4 fsin_Color;

      void main()
      {
          gl_Position = projection_matrix * vec4(Position, 0, 1);
          fsin_Color = Color;
      }";

    const string FragmentCode = @"
      #version 450

      layout(location = 0) in vec4 fsin_Color;
      layout(location = 0) out vec4 fsout_Color;

      void main()
      {
          fsout_Color = fsin_Color;
      }";

    DisposeGroup dispose = new DisposeGroup();

    public TestCanvas(GraphicsDevice gd, ImGuiView view, Func<Vector2> computeSize) : base(gd, view, computeSize)
    {
      Buffer = dispose.Add(new BufferList<VertexPositionColor>(gd, 6));

      VertexPositionColor[] quadVertices =
      {
        new VertexPositionColor(new Vector2(-150f, 150f), RgbaFloat.Red),
        new VertexPositionColor(new Vector2(150f, 150f), RgbaFloat.Green),
        new VertexPositionColor(new Vector2(-150f, -150f), RgbaFloat.Blue),
        new VertexPositionColor(new Vector2(150f, -150f), RgbaFloat.Yellow)
      };

      ushort[] quadIndices = { 0, 1, 2, 3 };

      VertexBuffer = Factory.CreateBuffer(new BufferDescription(4 * VertexPositionColor.SizeInBytes, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
      IndexBuffer = Factory.CreateBuffer(new BufferDescription(4 * sizeof(ushort), BufferUsage.IndexBuffer));
      dispose.Add(VertexBuffer);
      dispose.Add(IndexBuffer);

      ProjMatrix = Factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
      dispose.Add(ProjMatrix);

      GraphicsDevice.UpdateBuffer(VertexBuffer, 0, quadVertices);
      GraphicsDevice.UpdateBuffer(IndexBuffer, 0, quadIndices);

      VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
        new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4));

      Layout = Factory.CreateResourceLayout(new ResourceLayoutDescription(
          new ResourceLayoutElementDescription("ProjectionMatrixBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));
      dispose.Add(Layout);

      ShaderDescription vertexShaderDesc = new ShaderDescription(
        ShaderStages.Vertex,
        Encoding.UTF8.GetBytes(VertexCode),
        "main");
      ShaderDescription fragmentShaderDesc = new ShaderDescription(
        ShaderStages.Fragment,
        Encoding.UTF8.GetBytes(FragmentCode),
        "main");

      Shaders = dispose.AddArray(Factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc));

      var pipelineDescription = new GraphicsPipelineDescription();
      pipelineDescription.BlendState = BlendStateDescription.SingleOverrideBlend;
      pipelineDescription.DepthStencilState = new DepthStencilStateDescription(
        depthTestEnabled: true,
        depthWriteEnabled: true,
        comparisonKind: ComparisonKind.LessEqual);
      pipelineDescription.RasterizerState = new RasterizerStateDescription(
        cullMode: FaceCullMode.Front,
        fillMode: PolygonFillMode.Solid,
        frontFace: FrontFace.Clockwise,
        depthClipEnabled: true,
        scissorTestEnabled: false);
      pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleList;
      pipelineDescription.ResourceLayouts = new ResourceLayout[] { Layout };
      pipelineDescription.ShaderSet = new ShaderSetDescription(
        vertexLayouts: new VertexLayoutDescription[] { vertexLayout },
        shaders: Shaders);
      pipelineDescription.Outputs = Canvas.FrameBuffer.OutputDescription;

      Pipeline = Factory.CreateGraphicsPipeline(pipelineDescription);

      MainResourceSet = Factory.CreateResourceSet(new ResourceSetDescription(Layout, ProjMatrix));
    }

    protected override void ProcessInputs()
    {
      base.ProcessInputs();

      var rect = ImGui.GetItemRectSize();
      if(ImGui.IsItemHovered())
      {
        Console.WriteLine(ImGui.GetMousePos() - ImGui.GetCursorStartPos() - ImGui.GetWindowPos());
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
      }
    }

    protected override void RenderToCanvas(CommandList cl)
    {
      cl.SetFramebuffer(Canvas.FrameBuffer);

      cl.ClearColorTarget(0, RgbaFloat.Clear);

      Matrix4x4 mvp = Matrix4x4.Identity * Matrix4x4.CreateScale(2, 2, 1) * Matrix4x4.CreateScale(1.0f / this.Canvas.Width, 1.0f / this.Canvas.Height, 1) * Matrix4x4.CreateTranslation(-1, -1, 0) * Matrix4x4.CreateScale(1, -1, 1);

      GraphicsDevice.UpdateBuffer(ProjMatrix, 0, ref mvp);

      // cl.SetVertexBuffer(0, VertexBuffer);
      // cl.SetIndexBuffer(IndexBuffer, IndexFormat.UInt16);
      cl.SetPipeline(Pipeline);
      cl.SetGraphicsResourceSet(0, MainResourceSet);
      // cl.DrawIndexed(
      //     indexCount: 4,
      //     instanceCount: 1,
      //     indexStart: 0,
      //     vertexOffset: 0,
      //     instanceStart: 0);

      Buffer.Reset();

      for(int i = 0; i < 3; i++)
      {
        Buffer.Push(cl, new VertexPositionColor(new Vector2(0 + i * 100, 150f + i * 100), RgbaFloat.Red));
        Buffer.Push(cl, new VertexPositionColor(new Vector2(150f + i * 100, 150f + i * 100), RgbaFloat.Green));
        Buffer.Push(cl, new VertexPositionColor(new Vector2(0 + i * 100, 0 + i * 100), RgbaFloat.Blue));
        
        Buffer.Push(cl, new VertexPositionColor(new Vector2(150f + i * 100, 0 + i * 100), RgbaFloat.Yellow));
        Buffer.Push(cl, new VertexPositionColor(new Vector2(0 + i * 100, 0 + i * 100), RgbaFloat.Blue));
        Buffer.Push(cl, new VertexPositionColor(new Vector2(150f + i * 100, 150f + i * 100), RgbaFloat.Green));
      }

      Buffer.Flush(cl);
    }
  }
}
