/* ===============================================================================
 * Copyright (c) 2016 Pikkart S.r.l. All Rights Reserved.
 * Pikkart is a trademark of Pikkart S.r.l., registered in Europe,
 * the United States and other countries.
 *
 * This file is part of Pikkart AR SDK Tutorial series, a series of tutorials
 * explaining how to use and fully exploits Pikkart's AR SDK.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * ===============================================================================*/




using Android.Content.Res;
using Android.Graphics;
using Android.Opengl;
using Java.IO;
using Java.Nio;
using System;
using System.IO;
/**
* \class RenderUtils
* \brief Class containing various helper functions
*
* Class containing various helper functions
* Texture creation
* Shader creation
* Matrix operations
*/
namespace TutorialApp
{
    public class RenderUtils
    {
        /**
         * \brief Compile shader code.
         * @param shaderType type of shader (either GLES20.GL_VERTEX_SHADER or GLES20.GL_FRAGMENT_SHADER).
         * @param source source code of the shader.
         * @return gl shader id.
         */
        private static int initShader(int shaderType, string source)
        {
            int shader = GLES20.GlCreateShader(shaderType);
            if (shader != 0)
            {
                GLES20.GlShaderSource(shader, source);
                GLES20.GlCompileShader(shader);
                int[] glStatusVar = { GLES20.GlFalse };
                GLES20.GlGetShaderiv(shader, GLES20.GlCompileStatus, glStatusVar, 0);
                if (glStatusVar[0] == GLES20.GlFalse)
                {
                    //Log.e("RenderUtils", "initShader could NOT compile shader " + shaderType + " : " + GLES20.GlGetShaderInfoLog(shader));
                    GLES20.GlDeleteShader(shader);
                    shader = 0;
                }
            }
            return shader;
        }

        /**
         * \brief Create a gl shader from source code.
         * @param vertexShaderSrc vertex shader code.
         * @param fragmentShaderSrc fragment shader code.
         * @return gl program id.
         */
        public static int createProgramFromShaderSrc(string vertexShaderSrc, string fragmentShaderSrc)
        {
            int vertShader = initShader(GLES20.GlVertexShader, vertexShaderSrc);
            int fragShader = initShader(GLES20.GlFragmentShader, fragmentShaderSrc);
            if (vertShader == 0 || fragShader == 0)
            {
                return 0;
            }
            int program = GLES20.GlCreateProgram();
            if (program != 0)
            {
                GLES20.GlAttachShader(program, vertShader);
                GLES20.GlAttachShader(program, fragShader);
                GLES20.GlLinkProgram(program);
                int[] glStatusVar = { GLES20.GlFalse };
                GLES20.GlGetProgramiv(program, GLES20.GlLinkStatus, glStatusVar, 0);
                if (glStatusVar[0] == GLES20.GlFalse)
                {
                    //Log.e("RenderUtils", "createProgramFromShaderSrc could NOT link program : " + GLES20.GlGetProgramInfoLog(program));
                    GLES20.GlDeleteProgram(program);
                    program = 0;
                }
            }
            return program;
        }

        /**
         * \brief Load a texture from app assets and create related OpenGL structures.
         * @param assets app AssetManager.
         * @param fileName filepath (inside app assets dir) of the file to be loaded.
         * @param dims int array used to output texture dimensions (width, height).
         * @return gl texture id.
         */
        public static int loadTextureFromApk(AssetManager assets, string fileName, int[] dims)
        {
            Stream inputStream = null;
            try
            {
                inputStream = assets.Open(fileName, Access.Buffer);
                //BufferedInputStream bufferedStream = new BufferedInputStream(inputStream);
                Bitmap bitMap = BitmapFactory.DecodeStream(inputStream);
                //get data array
                int[] data = new int[bitMap.Width * bitMap.Height];
                bitMap.GetPixels(data, 0, bitMap.Width, 0, 0, bitMap.Width, bitMap.Height);
                dims[0] = bitMap.Width;
                dims[1] = bitMap.Height;
                return loadTextureFromIntBuffer(data, bitMap.Width, bitMap.Height);
            }
            catch (Exception e)
            {
                //Log.e("RenderUtils", "loadTextureFromApk failed to load texture '" + fileName + "' from APK with error " + e.getMessage());
                return -1;
            }
        }

        public static ByteBuffer loadTexture(AssetManager assets, string fileName, int[] dims)
        {
            Stream inputStream = null;
            try
            {
                inputStream = assets.Open(fileName, Access.Buffer);
                //BufferedInputStream bufferedStream = new BufferedInputStream(inputStream);
                Bitmap bitMap = BitmapFactory.DecodeStream(inputStream);

                int[] data = new int[bitMap.Width * bitMap.Height];
                bitMap.GetPixels(data, 0, bitMap.Width, 0, 0, bitMap.Width, bitMap.Height);
                dims[0] = bitMap.Width;
                dims[1] = bitMap.Height;

                // convert from int array to byte RGBA array
                int numPixels = bitMap.Width * bitMap.Height;
                byte[] dataBytes = new byte[numPixels * 4];
                for (int p = 0; p < numPixels; ++p)
                {
                    int colour = data[p];
                    dataBytes[p * 4] = (byte)(colour >> 16); // R
                    dataBytes[p * 4 + 1] = (byte)(colour >> 8); // G
                    dataBytes[p * 4 + 2] = (byte)colour; // B
                    dataBytes[p * 4 + 3] = (byte)(colour >> 24); // A
                }
                // put data into a direct memory byte buffer
                ByteBuffer bb_data = ByteBuffer.AllocateDirect(dataBytes.Length).Order(ByteOrder.NativeOrder());
                int rowSize = bitMap.Width * 4;
                for (int r = 0; r < bitMap.Height; r++)
                {
                    bb_data.Put(dataBytes, rowSize * (bitMap.Height - 1 - r), rowSize);
                }
                bb_data.Rewind();
                // cleans variables
                dataBytes = null;
                data = null;

                return bb_data;
            }
            catch (Exception e)
            {
                //Log.e("RenderUtils", "loadTextureFromApk failed to load texture '" + fileName + "' from APK with error " + e.getMessage());
                return null;
            }
        }

        public static int loadTextureFromByteBuffer(ByteBuffer data, int width, int height)
        {
            try
            {
                int[] gl_textureID = new int[1];
                GLES20.GlGenTextures(1, gl_textureID, 0);
                GLES20.GlBindTexture(GLES20.GlTexture2d, gl_textureID[0]);
                GLES20.GlTexParameterf(GLES20.GlTexture2d, GLES20.GlTextureMinFilter, GLES20.GlLinear);
                GLES20.GlTexParameterf(GLES20.GlTexture2d, GLES20.GlTextureMagFilter, GLES20.GlLinear);
                GLES20.GlTexImage2D(GLES20.GlTexture2d, 0, GLES20.GlRgba, width, height, 0, GLES20.GlRgba, GLES20.GlUnsignedByte, data);

                return gl_textureID[0];
            }
            catch (Exception e)
            {
                //Log.e("RenderUtils", "loadTextureFromApk failed to load texture '" + fileName + "' from APK with error " + e.getMessage());
                return -1;
            }
        }

        /**
         * \brief Load a texture from app assets and create related OpenGL structures.
         * @param assets app AssetManager.
         * @param fileName filepath (inside app assets dir) of the file to be loaded.
         * @return gl texture id.
         */
        public static int loadTextureFromApk(AssetManager assets, string fileName)
        {
            int[] dims = new int[2];
            return loadTextureFromApk(assets, fileName, dims);
        }

        /**
         * \brief Load a texture from a data buffer and create related OpenGL structures.
         * @param data texture data buffer as int array.
         * @param width texture width.
         * @param height texture height
         * @return gl texture id.
         */
        private static int loadTextureFromIntBuffer(int[] data, int width, int height)
        {
            // convert from int array to byte RGBA array
            int numPixels = width * height;
            byte[] dataBytes = new byte[numPixels * 4];
            for (int p = 0; p < numPixels; ++p)
            {
                int colour = data[p];
                dataBytes[p * 4] = (byte)(colour >> 16); // R
                dataBytes[p * 4 + 1] = (byte)(colour >> 8); // G
                dataBytes[p * 4 + 2] = (byte)colour; // B
                dataBytes[p * 4 + 3] = (byte)(colour >> 24); // A
            }
            // put data into a direct memory byte buffer
            ByteBuffer bb_data = ByteBuffer.AllocateDirect(dataBytes.Length).Order(ByteOrder.NativeOrder());
            int rowSize = width * 4;
            for (int r = 0; r < height; r++)
            {
                bb_data.Put(dataBytes, rowSize * (height - 1 - r), rowSize);
            }
            bb_data.Rewind();
            // cleans variables
            dataBytes = null;
            data = null;
            // create gl texture and upload data
            int[] gl_textureID = new int[1];
            GLES20.GlGenTextures(1, gl_textureID, 0);
            GLES20.GlBindTexture(GLES20.GlTexture2d, gl_textureID[0]);
            GLES20.GlTexParameterf(GLES20.GlTexture2d, GLES20.GlTextureMinFilter, GLES20.GlLinear);
            GLES20.GlTexParameterf(GLES20.GlTexture2d, GLES20.GlTextureMagFilter, GLES20.GlLinear);
            GLES20.GlTexImage2D(GLES20.GlTexture2d, 0, GLES20.GlRgba, width, height, 0, GLES20.GlRgba, GLES20.GlUnsignedByte, bb_data);

            return gl_textureID[0];
        }

        /**
         * \brief Create an external texture(GL_TEXTURE_EXTERNAL_OES) for video rendering.
         * @return gl texture id.
         */
        public static int createVideoTexture()
        {
            int[] gl_textureID = new int[1];
            GLES20.GlGenTextures(1, gl_textureID, 0);
            GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, gl_textureID[0]);
            GLES20.GlTexParameterf(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMinFilter, GLES20.GlLinear);
            GLES20.GlTexParameterf(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMagFilter, GLES20.GlLinear);
            GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, 0);
            return gl_textureID[0];
        }

        /**
         * \brief Fill matrix data with Identity 4x4 matrix values.
         * @param mat_data matrix data.
         */
        public static void matrix44Identity(float[] mat_data)
        {
            for (int i = 0; i < 16; i++)
            {
                mat_data[i] = 0.0f;
            }
            mat_data[0] = 1.0f;
            mat_data[5] = 1.0f;
            mat_data[10] = 1.0f;
            mat_data[15] = 1.0f;
        }

        /**
         * \brief Transpose 4x4 matrix m_in and put output values in m_out.
         * @param m_in matrix to transpose.
         * @param m_out transposed matrix.
         */
        public static void matrix44Transpose(float[] m_in, float[] m_out)
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    m_out[i * 4 + j] = m_in[i + 4 * j];
                }
            }
        }

        /**
         * \brief Multiply two matrices.
         * @param rows1 number of rows of the first matrix.
         * @param cols1 number of cols of the first matrix.
         * @param mat1 the first matrix
         * @param rows2 number of rows of the second matrix.
         * @param cols2 number of cols of the second matrix.
         * @param mat2 the second matrix
         * @param result the output matrix, product of mat1 and mat2
         * @return true on successful multiplication.
         */
        public static bool matrixMultiply(int rows1, int cols1, float[] mat1, int rows2, int cols2, float[] mat2, float[] result)
        {
            if (cols1 != rows2)
            {
                return false;
            }
            else
            {
                float tempResult;
                for (int i = 0; i < rows1; i++)
                {
                    for (int j = 0; j < cols2; j++)
                    {
                        tempResult = 0;
                        for (int k = 0; k < rows2; k++)
                        {
                            tempResult += mat1[i * cols1 + k] * mat2[k * cols2 + j];
                        }
                        result[i * cols2 + j] = tempResult;
                    }
                }
            }
            return true;
        }

        /**
         * \brief Multiply UV coords with 4x4 matrix.
         * @param u u coord.
         * @param v v coord.
         * @param mat the transform amtrix
         * @return float array with transformed coordinates.
         */
        public static float[] uvMultMat4f(float u, float v, float[] mat)
        {
            float x = mat[0] * u + mat[4] * v + mat[12] * 1.0f;
            float y = mat[1] * u + mat[5] * v + mat[13] * 1.0f;
            float[] result = new float[2];
            result[0] = x;
            result[1] = y;
            return result;
        }

        /**
         * \brief Print out OpenGL errors.
         * @param op last GL function exectured (used for tagging error logs).
         */
        public static void CheckGLError(string op)
        {
            for (int error = GLES20.GlGetError(); error != 0; error = GLES20.GlGetError())
            {
                //Log.e("Renderer", "After operation " + op + " got glError 0x" + Integer.toHexstring(error));
            }
        }
    }

}