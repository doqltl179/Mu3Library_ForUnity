#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor {
    public static class MeshUtilFunc {



        #region Utility
        public static void SetAlphamapWithSplatTextures(Terrain terr, List<Texture2D> textures, string textureNameFormat) {
            TerrainData terrainData = terr.terrainData;
            TerrainLayer[] layers = terrainData.terrainLayers;

            Texture2D[] usingTextures = new Texture2D[layers.Length];
            for(int i = 0; i < usingTextures.Length; i++) {
                string textureName = string.Format(textureNameFormat, layers[i].name);
                Texture2D st = textures.Where(t => t.name == textureName).FirstOrDefault();

                usingTextures[i] = st;
                if(st == null) {
                    Debug.Log($"Splat Texture not found. terrainName: {terr.name}, textureName: {textureName}");
                }
            }

            Texture2D tex = null;
            for(int i = 0; i < layers.Length; i++) {
                tex = usingTextures[i];
                if(tex == null) continue;

                // 알파맵의 크기를 가져옵니다.
                int alphamapWidth = terrainData.alphamapWidth;
                int alphamapHeight = terrainData.alphamapHeight;

                // 현재 알파맵을 가져옵니다.
                float[,,] alphaMaps = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

                Color col;
                int caseNum = i % 4;
                switch(caseNum) {
                    case 0: {
                            for(int y = 0; y < alphamapHeight; y++) {
                                for(int x = 0; x < alphamapWidth; x++) {
                                    col = tex.GetPixelBilinear((float)x / (alphamapWidth - 1), (float)y / (alphamapHeight - 1));
                                    alphaMaps[y, x, i] = col.r;
                                }
                            }
                        }
                        break;
                    case 1: {
                            for(int y = 0; y < alphamapHeight; y++) {
                                for(int x = 0; x < alphamapWidth; x++) {
                                    col = tex.GetPixelBilinear((float)x / (alphamapWidth - 1), (float)y / (alphamapHeight - 1));
                                    alphaMaps[y, x, i] = col.g;
                                }
                            }
                        }
                        break;
                    case 2: {
                            for(int y = 0; y < alphamapHeight; y++) {
                                for(int x = 0; x < alphamapWidth; x++) {
                                    col = tex.GetPixelBilinear((float)x / (alphamapWidth - 1), (float)y / (alphamapHeight - 1));
                                    alphaMaps[y, x, i] = col.b;
                                }
                            }
                        }
                        break;
                    case 3: {
                            for(int y = 0; y < alphamapHeight; y++) {
                                for(int x = 0; x < alphamapWidth; x++) {
                                    col = tex.GetPixelBilinear((float)x / (alphamapWidth - 1), (float)y / (alphamapHeight - 1));
                                    alphaMaps[y, x, i] = col.a;
                                }
                            }
                        }
                        break;
                }

                terrainData.SetAlphamaps(0, 0, alphaMaps);
            }
        }

        /// <summary>
        /// <br/> directory: systemPath
        /// <br/> return: saved files paths
        /// </summary>
        public static void SaveSplatTextures(Terrain terr, string directory, string fileNameFormat) {
            TerrainData terrainData = terr.terrainData;
            TerrainLayer[] layers = terrainData.terrainLayers;

            Texture2D alphamap = null;
            Texture2D channel = null;
            byte[] data = null;
            Color[] colors = null;
            Color[] newColors = null;
            int layerIndex;
            string fileName = "";
            string filePath = "";
            for(int i = 0; i < terrainData.alphamapTextureCount; i++) {
                alphamap = terrainData.GetAlphamapTexture(i);

                if(alphamap != null) {
                    colors = alphamap.GetPixels();

                    // 0: r
                    // 1: g
                    // 2: b
                    // 3: a
                    for(int j = 0; j < 4; j++) {
                        layerIndex = i * 4 + j;
                        if(layerIndex >= layers.Length) {
                            Debug.Log($"LayerIndex out of range. layerIndex: {layerIndex}, layerLength: {layers.Length}");

                            break;
                        }

                        newColors = null;
                        switch(j) {
                            case 0: newColors = colors.Select(t => Color.red * t.r).ToArray(); break;
                            case 1: newColors = colors.Select(t => Color.green * t.g).ToArray(); break;
                            case 2: newColors = colors.Select(t => Color.blue * t.b).ToArray(); break;
                            case 3: newColors = colors.Select(t => Color.white * t.a).ToArray(); break;
                        }

                        channel = new Texture2D(alphamap.width, alphamap.height, TextureFormat.RGBA32, false);
                        channel.SetPixels(newColors);
                        channel.Apply();

                        fileName = string.Format(fileNameFormat, layers[layerIndex].name);
                        filePath = Path.Combine(directory, fileName);

                        data = channel.EncodeToPNG();
                        File.WriteAllBytes(filePath, data);

                        //Debug.Log($"File Saved. path: {filePath}");
                    }
                }
                else {
                    Debug.Log($"Alphamap Texture not found. terrain: {terr.name}, index: {i}");
                }
            }
        }

        public static Mesh GetCombineMesh(List<Mesh> meshList, bool increaseMaxVertexCount = true) {
            Mesh result = new Mesh();
            if(increaseMaxVertexCount) {
                result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            List<Vector3> verts = new List<Vector3>();
            List<int[]> tris = new List<int[]>();
            List<Vector2> uvs = new List<Vector2>();
            for(int i = 0; i < meshList.Count; i++) {
                verts.AddRange(meshList[i].vertices);
                tris.Add(meshList[i].triangles);
                uvs.AddRange(meshList[i].uv);
            }

            result.vertices = verts.ToArray();

            result.subMeshCount = tris.Count;
            int triIndexOffset = 0;
            for(int i = 0; i < result.subMeshCount; i++) {
                triIndexOffset = 0;
                for(int j = 0; j < i; j++) {
                    triIndexOffset += meshList[j].vertices.Length;
                }

                result.SetTriangles(tris[i].Select(t => t + triIndexOffset).ToArray(), i);
            }

            result.uv = uvs.ToArray();

            result.RecalculateBounds();
            result.RecalculateNormals();
            result.RecalculateTangents();

            return result;
        }

        public static List<Mesh> CopyMeshsFromTerrainUsingTextureAlpha(Terrain terr, Texture2D tex, int resolution, bool increaseMaxVertexCount = true) {
            if(!CanCopyMeshFromTerrainUsingTextureAlpha(terr, tex)) {
                return null;
            }

            // ---------------------------------------------------------------------------
            // 터레인의 버텍스 배치 방식은 [Row, Column]을 기반으로 배치한다는 걸 꼭 기억할 것.
            // ---------------------------------------------------------------------------

            if(resolution > terr.terrainData.heightmapResolution) {
                resolution = terr.terrainData.heightmapResolution;
                Debug.Log($"Resolution Changed to 'terrainData.heightmapResolution'. request: {resolution}, heightmapResolution: {terr.terrainData.heightmapResolution}");
            }
            int vertCountR = resolution; //세로 버텍스 개수
            int vertCountC = resolution; //가로 버텍스 개수

            // 터레인에 적용되어 있는 각 위치의 높이 비율
            //float[,] heights = terr.terrainData.GetHeights(0, 0, vertCountC, vertCountR);
            float[,] heights = GetResolutionHeight(terr.terrainData, resolution);

            #region 터레인 버텍스 정보 생성

            TerrainVertexInfo[,] terrVertInfo = GetVertexInfosWithTextureAlpha(tex, heights, terr.terrainData.size, vertCountR, vertCountC);

            #endregion

            #region 텍스쳐 기반 청크 생성

            List<TerrainVertexInfo[,]> chunks = GetChunks(terrVertInfo, vertCountR, vertCountC);

            #endregion

            #region 메쉬 생성

            List<Mesh> meshes = CreateMeshAtEachMesh(chunks, vertCountR, vertCountC, increaseMaxVertexCount);

            #endregion

            return meshes;
        }

        public static Mesh CopyMeshFromTerrainUsingTextureAlpha(Terrain terr, Texture2D tex, int resolution, bool increaseMaxVertexCount = true) {
            if(!CanCopyMeshFromTerrainUsingTextureAlpha(terr, tex)) {
                return null;
            }

            // ---------------------------------------------------------------------------
            // 터레인의 버텍스 배치 방식은 [Row, Column]을 기반으로 배치한다는 걸 꼭 기억할 것.
            // ---------------------------------------------------------------------------

            if(resolution > terr.terrainData.heightmapResolution) {
                resolution = terr.terrainData.heightmapResolution;
                Debug.Log($"Resolution Changed to 'terrainData.heightmapResolution'. request: {resolution}, heightmapResolution: {terr.terrainData.heightmapResolution}");
            }
            int vertCountR = resolution; //세로 버텍스 개수
            int vertCountC = resolution; //가로 버텍스 개수

            // 터레인에 적용되어 있는 각 위치의 높이 비율
            //float[,] heights = terr.terrainData.GetHeights(0, 0, vertCountC, vertCountR);
            float[,] heights = GetResolutionHeight(terr.terrainData, resolution);

            #region 터레인 버텍스 정보 생성

            TerrainVertexInfo[,] terrVertInfo = GetVertexInfosWithTextureAlpha(tex, heights, terr.terrainData.size, vertCountR, vertCountC);

            #endregion

            #region 텍스쳐 기반 청크 생성

            List<TerrainVertexInfo[,]> chunks = GetChunks(terrVertInfo, vertCountR, vertCountC);

            #endregion

            #region 메쉬 생성

            Mesh mesh = CreateMeshAtOne(chunks, vertCountR, vertCountC, increaseMaxVertexCount);

            #endregion

            return mesh;
        }
        #endregion



        private static bool CanCopyMeshFromTerrainUsingTextureAlpha(Terrain terr, Texture2D tex) {
            if(terr == null || terr.terrainData == null || tex == null) {
                Debug.LogWarning("Data not enough.");

                return false;
            }
            else if(!tex.isReadable) {
                Debug.LogWarning("설정된 Texture의 'Read/Write' 설정을 체크(True)한 후 import하고 다시 시도해주세요.");

                return false;
            }

            return true;
        }

        private static Mesh CreateMeshAtOne(List<TerrainVertexInfo[,]> chunks, int vertCountR, int vertCountC, bool increaseMaxVertexCount = true) {
            Mesh result = new Mesh();
            if(increaseMaxVertexCount) {
                result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            List<List<Vector3>> verts = new List<List<Vector3>>();
            List<List<int>> tris = new List<List<int>>();
            List<List<Vector2>> uvs = new List<List<Vector2>>();

            TerrainVertexInfo[,] data;
            for(int i = 0; i < chunks.Count; i++) {
                data = chunks[i];

                // 'TerrainVertexInfo'로 만들어지는 vertices의 인덱스값을 빠르게 찾기 위함
                int[,] vertIndexMap = new int[vertCountR, vertCountC];

                // 버텍스 배열을 먼저 생성
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uv = new List<Vector2>();
                int vertIndex = 0;
                for(int r = 0; r < vertCountR; r++) {
                    for(int c = 0; c < vertCountC; c++) {
                        if(data[r, c] != null) {
                            vertices.Add(data[r, c].Pos);
                            uv.Add(data[r, c].UV);

                            vertIndexMap[r, c] = vertIndex;
                            vertIndex++;
                        }
                        else {
                            vertIndexMap[r, c] = -1;
                        }
                    }
                }

                List<int> triangles = new List<int>();
                int triIndexOffset = verts.Sum(t => t.Count);

                // v1, v2, v3에서 v1은 원점이며, v2, v3는 시계 방향순으로 입력한다.
                int v1, v2, v3;
                void AddTriangle() {
                    triangles.Add(v1 + triIndexOffset);
                    triangles.Add(v2 + triIndexOffset);
                    triangles.Add(v3 + triIndexOffset);
                }

                // 왼쪽 아래에서 오른쪽 위로 triangle 생성
                for(int r = 0; r < vertCountR - 1; r++) {
                    for(int c = 0; c < vertCountC - 1; c++) {
                        v1 = vertIndexMap[r, c];

                        if(v1 >= 0) {
                            if(r + 1 < vertCountR && c + 1 < vertCountC) {
                                // v2 --> 오른쪽 위
                                // v3 --> 오른쪽
                                v2 = vertIndexMap[r + 1, c + 1];
                                v3 = vertIndexMap[r, c + 1];
                                if(v2 >= 0 && v3 >= 0) {
                                    AddTriangle();
                                }

                                // v2 --> 위
                                // v3 --> 오른쪽 위
                                v2 = vertIndexMap[r + 1, c];
                                v3 = vertIndexMap[r + 1, c + 1];
                                if(v2 >= 0 && v3 >= 0) {
                                    AddTriangle();
                                }
                            }

                            // 비어있는 부분의 triangle을 메꿔주기 위함
                            #region Extra
                            if(r + 1 < vertCountR && c - 1 >= 0) {
                                if(vertIndexMap[r, c - 1] < 0) {
                                    v2 = vertIndexMap[r + 1, c - 1];
                                    v3 = vertIndexMap[r + 1, c];
                                    if(v2 >= 0 && v3 >= 0) {
                                        AddTriangle();
                                    }
                                }

                                if(vertIndexMap[r + 1, c] < 0) {
                                    v2 = vertIndexMap[r, c - 1];
                                    v3 = vertIndexMap[r + 1, c - 1];
                                    if(v2 >= 0 && v3 >= 0) {
                                        AddTriangle();
                                    }
                                }
                            }
                            #endregion
                        }
                    }
                }

                verts.Add(vertices);
                tris.Add(triangles);
                uvs.Add(uv);
            }

            List<Vector3> vertList = new List<Vector3>();
            for(int i = 0; i < verts.Count; i++) {
                vertList.AddRange(verts[i]);
            }
            result.vertices = vertList.ToArray();

            //result.subMeshCount = tris.Count;
            //for(int i = 0; i < tris.Count; i++) {
            //    result.SetTriangles(tris[i], i);
            //}
            List<int> triList = new List<int>();
            for(int i = 0; i < tris.Count; i++) {
                triList.AddRange(tris[i]);
            }
            result.triangles = triList.ToArray();

            List<Vector2> uvList = new List<Vector2>();
            for(int i = 0; i < uvs.Count; i++) {
                uvList.AddRange(uvs[i]);
            }
            result.uv = uvList.ToArray();

            result.RecalculateNormals();
            result.RecalculateBounds();
            result.RecalculateTangents();

            return result;
        }

        private static List<Mesh> CreateMeshAtEachMesh(List<TerrainVertexInfo[,]> chunks, int vertCountR, int vertCountC, bool increaseMaxVertexCount = true) {
            List<Mesh> result = new List<Mesh>();

            TerrainVertexInfo[,] data;
            for(int i = 0; i < chunks.Count; i++) {
                data = chunks[i];

                // 'TerrainVertexInfo'로 만들어지는 vertices의 인덱스값을 빠르게 찾기 위함
                int[,] vertIndexMap = new int[vertCountR, vertCountC];

                // 버텍스 배열을 먼저 생성
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uv = new List<Vector2>();
                int vertIndex = 0;
                for(int r = 0; r < vertCountR; r++) {
                    for(int c = 0; c < vertCountC; c++) {
                        if(data[r, c] != null) {
                            vertices.Add(data[r, c].Pos);
                            uv.Add(data[r, c].UV);

                            vertIndexMap[r, c] = vertIndex;
                            vertIndex++;
                        }
                        else {
                            vertIndexMap[r, c] = -1;
                        }
                    }
                }

                List<int> triangles = new List<int>();

                // v1, v2, v3에서 v1은 원점이며, v2, v3는 시계 방향순으로 입력한다.
                int v1, v2, v3;
                void AddTriangle() {
                    triangles.Add(v1);
                    triangles.Add(v2);
                    triangles.Add(v3);
                }

                // 왼쪽 아래에서 오른쪽 위로 triangle 생성
                for(int r = 0; r < vertCountR; r++) {
                    for(int c = 0; c < vertCountC; c++) {
                        v1 = vertIndexMap[r, c];

                        if(v1 >= 0) {
                            if(r + 1 < vertCountR && c + 1 < vertCountC) {
                                // v2 --> 오른쪽 위
                                // v3 --> 오른쪽
                                v2 = vertIndexMap[r + 1, c + 1];
                                v3 = vertIndexMap[r, c + 1];
                                if(v2 >= 0 && v3 >= 0) {
                                    AddTriangle();
                                }

                                // v2 --> 위
                                // v3 --> 오른쪽 위
                                v2 = vertIndexMap[r + 1, c];
                                v3 = vertIndexMap[r + 1, c + 1];
                                if(v2 >= 0 && v3 >= 0) {
                                    AddTriangle();
                                }
                            }

                            // 비어있는 부분의 triangle을 메꿔주기 위함
                            #region Extra
                            if(r + 1 < vertCountR && c - 1 >= 0) {
                                if(vertIndexMap[r, c - 1] < 0) {
                                    v2 = vertIndexMap[r + 1, c - 1];
                                    v3 = vertIndexMap[r + 1, c];
                                    if(v2 >= 0 && v3 >= 0) {
                                        AddTriangle();
                                    }
                                }

                                if(vertIndexMap[r + 1, c] < 0) {
                                    v2 = vertIndexMap[r, c - 1];
                                    v3 = vertIndexMap[r + 1, c - 1];
                                    if(v2 >= 0 && v3 >= 0) {
                                        AddTriangle();
                                    }
                                }
                            }
                            #endregion
                        }
                    }
                }

                Mesh mesh = new Mesh();
                if(increaseMaxVertexCount) {
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                mesh.vertices = vertices.ToArray();
                mesh.triangles = triangles.ToArray();
                mesh.uv = uv.ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();

                result.Add(mesh);
            }

            return result;
        }

        private static List<TerrainVertexInfo[,]> GetChunks(TerrainVertexInfo[,] vertices, int vcR, int vcC) {
            List<TerrainVertexInfo[,]> chunks = new List<TerrainVertexInfo[,]>();
            bool IsVertUsedOnOtherChunk(TerrainVertexInfo vert) {
                for(int i = 0; i < chunks.Count; i++) {
                    if((chunks[i])[vert.KeyR, vert.KeyC] != null) return true;
                }

                return false;
            }

            // 왼쪽 아래에서 오른쪽 위로 탐색
            for(int r = 0; r < vcR; r++) {
                for(int c = 0; c < vcC; c++) {
                    if(vertices[r, c] != null && !IsVertUsedOnOtherChunk(vertices[r, c])) {
                        TerrainVertexInfo[,] chunk = new TerrainVertexInfo[vcR, vcC];

                        List<TerrainVertexInfo> searchStorage = new List<TerrainVertexInfo>();
                        searchStorage.Add(vertices[r, c]); // 첫 탐색 버텍스를 미리 저장

                        bool[,] checkingList = new bool[vcR, vcC];
                        checkingList[searchStorage[0].KeyR, searchStorage[0].KeyC] = true;

                        void AddInSearchStorage(int row, int col) {
                            if(!checkingList[row, col] && chunk[row, col] == null && vertices[row, col] != null) {
                                TerrainVertexInfo checkVert = vertices[row, col];
                                searchStorage.Add(checkVert);

                                checkingList[checkVert.KeyR, checkVert.KeyC] = true;
                            }
                        }

                        int aroundKeyR, aroundKeyC;
                        while(searchStorage.Count > 0) {
                            TerrainVertexInfo currentSearchingVert = searchStorage[0];
                            chunk[currentSearchingVert.KeyR, currentSearchingVert.KeyC] = currentSearchingVert;

                            // 오른쪽으로 이어져 있는지 확인
                            aroundKeyR = currentSearchingVert.KeyR;
                            aroundKeyC = currentSearchingVert.KeyC + 1;
                            if(aroundKeyC < vcC) {
                                AddInSearchStorage(aroundKeyR, aroundKeyC);
                            }

                            // 위로 이어져 있는지 확인
                            aroundKeyR = currentSearchingVert.KeyR + 1;
                            aroundKeyC = currentSearchingVert.KeyC;
                            if(aroundKeyR < vcR) {
                                AddInSearchStorage(aroundKeyR, aroundKeyC);
                            }

                            // 왼쪽으로 이어져 있는지 확인
                            aroundKeyR = currentSearchingVert.KeyR;
                            aroundKeyC = currentSearchingVert.KeyC - 1;
                            if(aroundKeyC >= 0) {
                                AddInSearchStorage(aroundKeyR, aroundKeyC);
                            }

                            // 아래로 이어져 있는지 확인
                            aroundKeyR = currentSearchingVert.KeyR - 1;
                            aroundKeyC = currentSearchingVert.KeyC;
                            if(aroundKeyR >= 0) {
                                AddInSearchStorage(aroundKeyR, aroundKeyC);
                            }

                            searchStorage.RemoveAt(0);
                        }

                        chunks.Add(chunk);
                    }
                }
            }
            //Debug.Log($"Find Chunk Count: {chunks.Count}");

            return chunks;
        }

        private static TerrainVertexInfo[,] GetVertexInfosWithTextureAlpha(Texture2D tex, float[,] heights, Vector3 terrSize, int vcR, int vcC) {
            float ratioU, ratioV;
            Color uvCol;

            TerrainVertexInfo[,] terrVertInfo = new TerrainVertexInfo[vcR, vcC];
            for(int r = 0; r < vcR; r++) {
                for(int c = 0; c < vcC; c++) {
                    ratioU = (float)c / (vcC - 1);
                    ratioV = (float)r / (vcR - 1);

                    // ----------------------------------- Error -----------------------------------
                    // 'xxx' is not readable, the texture memory can not be accessed from scripts.
                    // You can make the texture readable in the Texture Import Settings.
                    // -----------------------------------------------------------------------------
                    //
                    // 간혹, 위와 같이 에러 메시지가 나올 때가 있는데
                    // 이 때에는 Texture의 'Read/Write'를 체크하고 import한 후에 다시 시도하자.
                    uvCol = tex.GetPixelBilinear(Mathf.Clamp01(ratioU - 0.001f), Mathf.Clamp01(ratioV - 0.001f));

                    if(uvCol.a > 0) {
                        terrVertInfo[r, c] = new TerrainVertexInfo(
                            r,
                            c,
                            new Vector3(ratioU * terrSize.x, heights[r, c] * terrSize.y, ratioV * terrSize.z),
                            new Vector2(ratioU, ratioV));
                    }
                }
            }

            return terrVertInfo;
        }

        private static float[,] GetResolutionHeight(TerrainData terrData, int newResolution) {
            float[,] heights = terrData.GetHeights(0, 0, terrData.heightmapResolution, terrData.heightmapResolution);
            if(terrData.heightmapResolution == newResolution) {
                return heights;
            }

            float[,] newHeights = new float[newResolution, newResolution];

            // 기존 해상도와 새로운 해상도의 비율 계산
            float resolutionRatio = (float)terrData.heightmapResolution / newResolution;

            // 새 해상도에 맞게 데이터를 리샘플링 (선형 보간)
            for(int y = 0; y < newResolution; y++) {
                for(int x = 0; x < newResolution; x++) {
                    // 기존 좌표계에서의 위치를 계산 (비율에 따라 스케일링)
                    float originalX = x * resolutionRatio;
                    float originalY = y * resolutionRatio;

                    // 좌표를 정수 인덱스로 변환
                    int floorX = Mathf.FloorToInt(originalX);
                    int floorY = Mathf.FloorToInt(originalY);

                    // 경계값 처리 (인덱스가 배열 크기를 초과하지 않도록)
                    floorX = Mathf.Clamp(floorX, 0, terrData.heightmapResolution - 1);
                    floorY = Mathf.Clamp(floorY, 0, terrData.heightmapResolution - 1);

                    // 높이 값을 새로운 좌표에 맞게 할당
                    newHeights[y, x] = heights[floorY, floorX]; // 2차원 배열로 접근
                }
            }

            return newHeights;
        }

        private class TerrainVertexInfo {
            public int KeyR => keyR;
            private int keyR = -1;

            public int KeyC => keyC;
            private int keyC = -1;

            public Vector3 Pos => pos;
            private Vector3 pos = Vector3.zero;

            public Vector2 UV => uv;
            private Vector2 uv = Vector2.zero;



            public TerrainVertexInfo(int kR, int kC, Vector3 p, Vector2 uv) {
                keyR = kR;
                keyC = kC;
                pos = p;
                this.uv = uv;
            }
        }
    }
}
#endif