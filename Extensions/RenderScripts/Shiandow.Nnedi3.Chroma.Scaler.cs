﻿// This file is a part of MPDN Extensions.
// https://github.com/zachsaw/MPDN_Extensions
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library.

using System;
using Mpdn.Extensions.Framework.RenderChain;
using Mpdn.Extensions.RenderScripts.Shiandow.NNedi3.Filters;
using Mpdn.RenderScript;
using SharpDX;

namespace Mpdn.Extensions.RenderScripts
{
    namespace Shiandow.NNedi3.Chroma
    {
        public class NNedi3Chroma : ChromaChain
        {
            #region Settings

            public NNedi3Chroma()
            {
                Neurons1 = NNedi3Neurons.Neurons16;
                Neurons2 = NNedi3Neurons.Neurons16;
                CodePath = NNedi3Path.ScalarMad;
                Structured = false;
            }

            public NNedi3Neurons Neurons1 { get; set; }
            public NNedi3Neurons Neurons2 { get; set; }
            public NNedi3Path CodePath { get; set; }
            public bool Structured { get; set; }

            #endregion

            private static readonly int[] s_NeuronCount = {16, 32, 64, 128, 256};
            private static readonly string[] s_CodePath = {"A", "B", "C", "D", "E"};

            public override string Description
            {
                get
                {
                    return string.Format("{0} {1}/{2}", base.Description, s_NeuronCount[(int) Neurons1],
                        s_NeuronCount[(int) Neurons2]);
                }
            }

            protected override string ShaderPath
            {
                get { return typeof (NNedi3).Name; }
            }

            private NNedi3Shader GetShader(NNedi3Neurons neurons, bool u)
            {
                var filename = string.Format("NNEDI3_{3}_{0}_{1}{2}.cso", s_NeuronCount[(int) neurons], s_CodePath[(int) CodePath],
                    Structured ? "_S" : string.Empty, u ? "u" : "v");
                return new NNedi3Shader(FromByteCode(filename))
                {
                    Neurons = neurons,
                    Structured = Structured
                };
            }

            public override ITextureFilter ScaleChroma(ICompositionFilter composition)
            {
                if (!Renderer.IsDx11Avail)
                {
                    Renderer.FallbackOccurred = true; // Warn user via player stats OSD
                    return composition; // DX11 is not available; fallback
                }

                var luma = composition.Luma;
                var chroma = composition.Chroma;

                var lumaSize = luma.Size();
                var chromaSize = chroma.Size();

                if (lumaSize.Width != 2 * chromaSize.Width || lumaSize.Height != 2 * chromaSize.Height)
                    return composition; // Chroma shouldn't be doubled; fallback

                Func<TextureSize, TextureSize> transform = s => new TextureSize(2 * s.Height, s.Width);

                var shaderUPass1 = GetShader(Neurons1, true);
                var shaderUPass2 = GetShader(Neurons2, true);
                var shaderVPass1 = GetShader(Neurons1, false);
                var shaderVPass2 = GetShader(Neurons2, false);

                var interleaveU = new Shader(FromFile("Interleave.hlsl", compilerOptions: "CHROMA_U=1")) { Transform = transform };
                var interleaveV = new Shader(FromFile("Interleave.hlsl", compilerOptions: "CHROMA_V=1")) { Transform = transform };

                var resultU = interleaveU.ApplyTo(chroma , chroma .Apply(shaderUPass1));
                var u       = interleaveU.ApplyTo(resultU, resultU.Apply(shaderUPass2));

                var resultV = interleaveV.ApplyTo(chroma , chroma .Apply(shaderVPass1));
                var v       = interleaveV.ApplyTo(resultV, resultU.Apply(shaderVPass2));

                return luma.MergeWith(u, v).ConvertToRgb();
            }
        }

        public class NNedi3ChromaScaler : RenderChainUi<NNedi3Chroma, NNedi3ChromaConfigDialog>
        {
            protected override string ConfigFileName
            {
                get { return "Shiandow.NNedi3Chroma"; }
            }

            public override string Category
            {
                get { return "Chroma Scaling"; }
            }

            public override ExtensionUiDescriptor Descriptor
            {
                get
                {
                    return new ExtensionUiDescriptor
                    {
                        Guid = new Guid("994C176F-AB9F-47E0-81FE-DC20609A40C2"),
                        Name = "NNEDI3 Chroma Doubler",
                        Description = "NNEDI3 chroma doubler",
                        Copyright = "Adapted by Shiandow and Zachs for MPDN"
                    };
                }
            }
        }
    }
}
