﻿using Catalyst.Allocation;
using Catalyst.Tools;
using Silk.NET.Vulkan;

namespace Catalyst.Engine.Graphics;

public unsafe class Texture : IDisposable
{
    private readonly GraphicsDevice _device;
    
    private AllocatedImage _allocatedImage;
    private ImageView _imageView;
    private Sampler _sampler;
    private  DescriptorSetLayout _descriptorSetLayout;
    private DescriptorSet _descriptorSet;
    private Extent3D ImageExtent => new(Width, Height, 1);

    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public readonly Format ImageFormat;
    
    public Image Image => _allocatedImage.Image;
    public DescriptorSetLayout DescriptorSetLayout => _descriptorSetLayout;
    public DescriptorSet DescriptorSet => _descriptorSet;

    //TOOD: Set by File
    public Texture(GraphicsDevice device, uint width, uint height, Format imageFormat, void* data)
    {
        _device = device;
        Width = width;
        Height = height;
        ImageFormat = imageFormat;
        AllocateImage();
        if(data != null)
            SetData(data);
    }

    private void AllocateImage()
    {
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = ImageFormat,
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
            Extent = ImageExtent
        };
        _allocatedImage = _device.CreateImage(imageInfo, MemoryPropertyFlags.DeviceLocalBit);
        var imageViewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _allocatedImage.Image,
            ViewType = ImageViewType.Type2D,
            Format = ImageFormat,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _imageView = _device.CreateImageView(imageViewInfo);
        
        var samplerCreateInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MinLod = -1000,
            MaxLod = 1000,
            MaxAnisotropy = 1.0f
        };
        _sampler = _device.CreateSampler(samplerCreateInfo);
        _descriptorSetLayout = DescriptorSetLayoutBuilder
            .Start()
            .WithSampler(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.FragmentBit, _sampler)
            .CreateOn(_device.Device);
    }

    public void SetData(void* data)
    {
        using var stagingBuffer = _device.CreateBuffer(FormatTools.SizeOf(ImageFormat), Width * Height, BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit);
        stagingBuffer.Map().Validate();
        stagingBuffer.WriteToBuffer(data);
        stagingBuffer.Flush().Validate();
        stagingBuffer.Unmap();
        _device.TransitionImageLayout(_allocatedImage.Image, ImageFormat, ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal, 1, 1);
        _device.CopyBufferToImage(stagingBuffer, _allocatedImage.Image, ImageLayout.TransferDstOptimal, ImageFormat, ImageExtent);
        _device.TransitionImageLayout(_allocatedImage.Image, ImageFormat, ImageLayout.TransferDstOptimal,
            ImageLayout.ShaderReadOnlyOptimal, 1, 1);
    }

    public void BindAsUIImage()
    {
        _descriptorSet = Application.ctx().BindAsUIImage(this, 1);
    }
    
    public DescriptorImageInfo ImageInfo => new()
    {
        Sampler = _sampler,
        ImageView = _imageView,
        ImageLayout = ImageLayout.ShaderReadOnlyOptimal
    };
    
    public void Dispose()
    {
        _device.DestroyImage(_allocatedImage);
        _device.DestroyImageView(_imageView);
        _device.DestroySampler(_sampler);
        _descriptorSetLayout.Dispose();
        GC.SuppressFinalize(this);
    }
}
