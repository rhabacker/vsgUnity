﻿using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using vsgUnity.Native;
using System.Runtime.InteropServices;

namespace vsgUnity
{
    public struct ShaderStageInfo : IEquatable<ShaderStageInfo>
    {
        public int id;
        public VkShaderStageFlagBits stages;
        public IntArray specializationData;
        public string customDefines;
        public string source;

        public bool Equals(ShaderStageInfo b)
        {
            return stages == b.stages &&
                source == b.source &&
                customDefines == b.customDefines &&
                specializationData.Equals(b.specializationData);
        }

        public ShaderStageData ToNative()
        {
            ShaderStageData s = new ShaderStageData
            {
                id = id,
                stages = stages,
                specializationData = NativeUtils.ToNative(specializationData),
                customDefines = NativeUtils.ToNative(customDefines),
                source = NativeUtils.ToNative(source)
            };
            return s;
        }
    }

    public struct ShaderStagesInfo : IEquatable<ShaderStagesInfo>
    {
        public int id;
        public ShaderStageInfo[] stages;
        public int stagesCount;

        public bool Equals(ShaderStagesInfo b)
        {
            if (stages == null && b.stages == null) return true;
            if (stages == null || b.stages == null) return false;
            return stagesCount == b.stagesCount &&
                stages.SequenceEqual<ShaderStageInfo>(b.stages);
        }

        public ShaderStagesData ToNative()
        {
            ShaderStagesData s = new ShaderStagesData
            {
                id = id,
                stagesCount = stagesCount
            };
            s.stages = new ShaderStageData[stagesCount];
            for(int i = 0; i < stagesCount; i++)
            {
                s.stages[i] = stages[i].ToNative();
            }
            return s;
        }
    }

    /// <summary>
    /// MaterialInfo
    /// All the native data representing a unity material, this struct is used to generate pipleline, shader and descriptor
    /// export commands, as well as being used in the cache.
    /// </summary>

    public class MaterialInfo
    {
        public int id;
        public ShaderMapping mapping;
        public ShaderStagesInfo shaderStages;
        public List<DescriptorImageData> imageDescriptors = new List<DescriptorImageData>();
        public List<DescriptorFloatUniformData> floatDescriptors = new List<DescriptorFloatUniformData>();
        public List<DescriptorVectorUniformData> vectorDescriptors = new List<DescriptorVectorUniformData>();
        public List<VkDescriptorSetLayoutBinding> descriptorBindings = new List<VkDescriptorSetLayoutBinding>();
        public List<string> customDefines = new List<string>();
        public int useAlpha;
    }

    /// <summary>
    /// MaterialConverter
    /// Helper class for converting Unity Materials and Textures to vsgUnity data structs for passing to the exporter
    /// </summary>
    
    public static class MaterialConverter
    {
        public static Dictionary<int, MaterialInfo> _materialDataCache = new Dictionary<int, MaterialInfo>();
        public static Dictionary<int, DescriptorImageData> _descriptorImageDataCache = new Dictionary<int, DescriptorImageData>();
        public static Dictionary<int, ShaderStageInfo> _shaderStageInfoCache = new Dictionary<int, ShaderStageInfo>();
        public static Dictionary<int, ShaderStagesInfo> _shaderStagesInfoCache = new Dictionary<int, ShaderStagesInfo>();

        public static void ClearCaches()
        {
            _materialDataCache.Clear();
            _descriptorImageDataCache.Clear();
            _shaderStageInfoCache.Clear();
            _shaderStagesInfoCache.Clear();
        }

        /// <summary>
        /// Get a imag data to match the passed material if one exisits in the cache otherwise create a new one
        /// </summary>
        /// <param name="imageData"></param>
        /// <param name="binding"></param>
        /// <returns></returns>

        public static DescriptorImageData GetOrCreateDescriptorImageData(ImageData imageData, int binding)
        {
            // see if we have one already
            foreach (int idkey in _descriptorImageDataCache.Keys)
            {
                DescriptorImageData did = _descriptorImageDataCache[idkey];
                if (did.binding == binding && did.image.id == imageData.id)
                {
                    return did;
                }
            }
            // must be new
            DescriptorImageData descriptorImage = new DescriptorImageData
            {
                id = _descriptorImageDataCache.Count,
                binding = binding,
                image = imageData
            };

            _descriptorImageDataCache[descriptorImage.id] = descriptorImage;

            return descriptorImage;
        }

        /// <summary>
        /// Get a shader stage data to match the passed material if one exisits in the cache otherwise create a new one
        /// </summary>
        /// <param name="shaderResource"></param>
        /// <param name="customDefines"></param>
        /// <param name="specializationContants"></param>
        /// <returns></returns>

        public static ShaderStageInfo GetOrCreateShaderStageInfo(ShaderResource shaderResource, string customDefines, int[] specializationContants)
        {
            ShaderStageInfo shaderStage = new ShaderStageInfo
            {
                source = shaderResource.sourceFile,
                stages = shaderResource.stages,
                customDefines = customDefines,
                specializationData = NativeUtils.WrapArray(specializationContants)
            };

            // see if we have one that matches
            foreach (int idkey in _shaderStageInfoCache.Keys)
            {
                ShaderStageInfo ssd = _shaderStageInfoCache[idkey];
                if (ssd.Equals(shaderStage))
                {
                    return ssd;
                }
            }

            // must be new
            shaderStage.id = _shaderStageInfoCache.Count;
            _shaderStageInfoCache[shaderStage.id] = shaderStage;

            return shaderStage;
        }

        /// <summary>
        /// Get a shader stages data to match the passed material if one exisits in the cache otherwise create a new one
        /// </summary>
        /// <param name="shaderResources"></param>
        /// <param name="customDefines"></param>
        /// <param name="specializationContants"></param>
        /// <returns></returns>

        public static ShaderStagesInfo GetOrCreateShaderStagesInfo(ShaderResource[] shaderResources, string customDefines, int[] specializationContants)
        {
            List<ShaderStageInfo> stages = new List<ShaderStageInfo>();
            foreach (ShaderResource shaderResource in shaderResources)
            {
                stages.Add(GetOrCreateShaderStageInfo(shaderResource, customDefines, specializationContants));
            }

            ShaderStagesInfo shaderStages = new ShaderStagesInfo
            {
                stages = stages.ToArray(),
                stagesCount = stages.Count
            };

            // see if we have one that matches
            foreach (int idkey in _shaderStagesInfoCache.Keys)
            {
                ShaderStagesInfo ssd = _shaderStagesInfoCache[idkey];
                if (ssd.Equals(shaderStages))
                {
                    return ssd;
                }
            }

            // must be new
            shaderStages.id = _shaderStagesInfoCache.Count;
            _shaderStagesInfoCache[shaderStages.id] = shaderStages;

            return shaderStages;
        }

        /// <summary>
        /// Get a material data to match the passed material if one exisits in the cache otherwise create a new one
        /// </summary>
        /// <param name="material"></param>
        /// <returns></returns>

        public static MaterialInfo GetOrCreateMaterialData(Material material)
        {
            if(_materialDataCache.ContainsKey(material.GetInstanceID()))
            {
                return _materialDataCache[material.GetInstanceID()];
            }
            else
            {
                return CreateMaterialData(material);
            }
        }

        /// <summary>
        /// Create a new material data based off of the pass material also adds the new data to the cache
        /// </summary>
        /// <param name="material"></param>
        /// <returns></returns>
        
        public static MaterialInfo CreateMaterialData(Material material)
        {
            // fetch the shadermapping for this materials shader
            ShaderMapping mapping = ShaderMappingIO.ReadFromJsonFile<ShaderMapping>(material.shader);
            if(mapping == null) // if we fail to find a mapping try and load the default
            {
                mapping = ShaderMappingIO.ReadFromJsonFile<ShaderMapping>(ShaderMappingIO.GetPathForShaderMappingFile("Default"));
            }

            MaterialInfo matdata = new MaterialInfo
            {
                id = material.GetInstanceID(),
                mapping = mapping
            };

            Debug.Log("shader name: " + material.shader.name);

            // process uniforms
            UniformMappedData[] uniformDatas = mapping.GetUniformDatasFromMaterial(material);

            foreach (UniformMappedData uniData in uniformDatas)
            {
                VkDescriptorType descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_MAX_ENUM;

                if (uniData.mapping.uniformType == UniformMapping.UniformType.Texture2DUniform)
                {
                    Texture tex = uniData.data as Texture;
                    if (tex == null) continue;

                    // get imagedata for the texture
                    ImageData imageData = TextureConverter.GetOrCreateImageData(tex);
                    // get descriptor for the image data
                    DescriptorImageData descriptorImage = GetOrCreateDescriptorImageData(imageData, uniData.mapping.vsgBindingIndex);
                    matdata.imageDescriptors.Add(descriptorImage);

                    descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
                }
                else if (uniData.mapping.uniformType == UniformMapping.UniformType.FloatUniform)
                {
                    float value = (float)uniData.data;
                    if (value == float.NaN) continue;

                    // get descriptor for the image data
                    DescriptorFloatUniformData descriptorFloat = new DescriptorFloatUniformData
                    {
                        id = 0,
                        binding = uniData.mapping.vsgBindingIndex,
                        value = value
                    };
                    matdata.floatDescriptors.Add(descriptorFloat);

                    descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
                }
                else if(uniData.mapping.uniformType == UniformMapping.UniformType.Vec4Uniform || uniData.mapping.uniformType == UniformMapping.UniformType.ColorUniform)
                {
                    Vector4 vector = Vector4.zero;
                    if(uniData.mapping.uniformType == UniformMapping.UniformType.Vec4Uniform) vector = (Vector4)uniData.data;
                    if (uniData.mapping.uniformType == UniformMapping.UniformType.ColorUniform)
                    {
                        Color color = (Color)uniData.data;
                        vector = new Vector4(color.r, color.g, color.b, color.a);
                    }
                    if (vector == null) continue;

                    // get descriptor for the image data
                    DescriptorVectorUniformData descriptorVector = new DescriptorVectorUniformData
                    {
                        id = 0,
                        binding = uniData.mapping.vsgBindingIndex,
                        value = vector
                    };
                    matdata.vectorDescriptors.Add(descriptorVector);

                    descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
                }

                if (descriptorType == VkDescriptorType.VK_DESCRIPTOR_TYPE_MAX_ENUM) continue;

                // add any custom defines related to the texture
                if (uniData.mapping.vsgDefines != null && uniData.mapping.vsgDefines.Count > 0) matdata.customDefines.AddRange(uniData.mapping.vsgDefines);

                if (descriptorType == VkDescriptorType.VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER) // hack for now only add the image samplers
                {
                    // create the descriptor binding to match the descriptor image
                    VkDescriptorSetLayoutBinding descriptorBinding = new VkDescriptorSetLayoutBinding
                    {
                        binding = (uint)uniData.mapping.vsgBindingIndex,
                        descriptorType = descriptorType,
                        descriptorCount = 1,
                        stageFlags = uniData.mapping.stages,
                        pImmutableSamplers = System.IntPtr.Zero
                    };
                    matdata.descriptorBindings.Add(descriptorBinding);
                }
            }

            string rendertype = material.GetTag("RenderType", true, "Opaque");
            matdata.useAlpha = rendertype.Contains("Transparent") ? 1 : 0;
            if (matdata.useAlpha == 1) matdata.customDefines.Add("VSG_BLEND");

            string lightmode = material.GetTag("LightMode", true, "ForwardBase");
            if (lightmode != "Always") matdata.customDefines.Add("VSG_LIGHTING");

            // lastly process shaders now we know the defines etc it will use
            string customDefinesStr = string.Join(",", matdata.customDefines.ToArray());

            matdata.shaderStages = GetOrCreateShaderStagesInfo(mapping.shaders.ToArray(), customDefinesStr, null);

            // add to the cache (double check it doesn't exist already)
            if(!_materialDataCache.ContainsKey(matdata.id))
            {
                _materialDataCache[matdata.id] = matdata;
            }

            return matdata;
        }

        public static Texture[] GetValidTexturesForMaterial(Material material)
        {
            List<Texture> results = new List<Texture>();
            string[] texnames = material.GetTexturePropertyNames();

            foreach (string texname in texnames)
            {
                Texture tex = material.GetTexture(texname);
                if (tex != null) results.Add(tex);
            }
            return results.ToArray();
        }
    }
}