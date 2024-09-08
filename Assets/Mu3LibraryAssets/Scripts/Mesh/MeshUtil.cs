using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.MeshUtil {
    public static class MeshUtilFunc {



        #region Utility
        public static void CutMeshInTerrainUsingTextureAlpha(Terrain terr, Texture2D tex) {
            if(terr == null || terr.terrainData == null || tex == null) {
                Debug.LogWarning("Data not enough.");

                return;
            }
            else if(!tex.isReadable) {
                Debug.LogError("설정된 Texture의 'Read/Write' 설정을 체크(True)한 후 import하고 다시 시도해주세요.");

                return;
            }

            // ---------------------------------------------------------------------------
            // 터레인의 버텍스 배치 방식은 [Row, Column]을 기반으로 배치한다는 걸 꼭 기억할 것.
            // ---------------------------------------------------------------------------

            int vertCountR = terr.terrainData.heightmapResolution; //세로 버텍스 개수
            int vertCountC = terr.terrainData.heightmapResolution; //가로 버텍스 개수
            float terrLengthX = terr.terrainData.size.x; //가로 길이
            float terrLengthZ = terr.terrainData.size.z; //세로 길이
            float terrLengthY = terr.terrainData.size.y; //높이 길이

            // 터레인에 적용되어 있는 각 위치의 높이 비율
            float[,] heights = terr.terrainData.GetHeights(0, 0, vertCountC, vertCountR);

            #region 터레인 버텍스 정보 생성
            float ratioU, ratioV;
            Color uvCol;

            TerrainVertexInfo[,] terrVertInfo = new TerrainVertexInfo[vertCountR, vertCountC];
            for(int r = 0; r < vertCountR; r++) {
                for(int c = 0; c < vertCountC; c++) {
                    ratioU = (float)c / vertCountC;
                    ratioV = (float)r / vertCountR;

                    // ----------------------------------- Error -----------------------------------
                    // 'xxx' is not readable, the texture memory can not be accessed from scripts.
                    // You can make the texture readable in the Texture Import Settings.
                    // -----------------------------------------------------------------------------
                    //
                    // 간혹, 위와 같이 에러 메시지가 나올 때가 있는데
                    // 이 때에는 Texture의 'Read/Write'를 체크하고 import한 후에 다시 시도하자.
                    uvCol = tex.GetPixelBilinear(ratioU, ratioV);

                    if(uvCol.a > 0) {
                        terrVertInfo[r, c] = new TerrainVertexInfo(r, c, new Vector3(ratioU * terrLengthX, heights[r, c] * terrLengthY, ratioV * terrLengthZ));
                    }
                }
            }
            #endregion

            #region 텍스쳐 기반 청크 생성
            List<TerrainVertexInfo[,]> chunks = new List<TerrainVertexInfo[,]>();
            bool IsVertUsedOnOtherChunk(TerrainVertexInfo vert) {
                for(int i = 0; i < chunks.Count; i++) {
                    if((chunks[i])[vert.KeyR, vert.KeyC] != null) return true;
                }

                return false;
            }

            // 왼쪽 아래에서 오른쪽 위로 탐색
            for(int r = 0; r < vertCountR; r++) {
                for(int c = 0; c < vertCountC; c++) {
                    if(terrVertInfo[r, c] != null && !IsVertUsedOnOtherChunk(terrVertInfo[r, c])) {
                        TerrainVertexInfo[,] chunk = new TerrainVertexInfo[vertCountR, vertCountC];

                        List<TerrainVertexInfo> searchStorage = new List<TerrainVertexInfo>();
                        searchStorage.Add(terrVertInfo[r, c]); // 첫 탐색 버텍스를 미리 저장

                        bool[,] checkingList = new bool[vertCountR, vertCountR];
                        checkingList[searchStorage[0].KeyR, searchStorage[0].KeyC] = true;

                        void AddInSearchStorage(int row, int col) {
                            if(!checkingList[row, col] && chunk[row, col] == null && terrVertInfo[row, col] != null) {
                                TerrainVertexInfo checkVert = terrVertInfo[row, col];
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
                            if(aroundKeyC < vertCountC) {
                                AddInSearchStorage(aroundKeyR, aroundKeyC);
                            }

                            // 위로 이어져 있는지 확인
                            aroundKeyR = currentSearchingVert.KeyR + 1;
                            aroundKeyC = currentSearchingVert.KeyC;
                            if(aroundKeyR < vertCountR) {
                                AddInSearchStorage(aroundKeyR, aroundKeyC);
                            }

                            // 왼쪽으로 이어져 있는지 확인
                            aroundKeyR = currentSearchingVert.KeyR;
                            aroundKeyC = currentSearchingVert.KeyC - 1;
                            if(aroundKeyC > 0) {
                                AddInSearchStorage(aroundKeyR, aroundKeyC);
                            }

                            // 아래로 이어져 있는지 확인
                            aroundKeyR = currentSearchingVert.KeyR - 1;
                            aroundKeyC = currentSearchingVert.KeyC;
                            if(aroundKeyR > 0) {
                                AddInSearchStorage(aroundKeyR, aroundKeyC);
                            }

                            searchStorage.RemoveAt(0);
                        }

                        chunks.Add(chunk);
                    }
                }
            }
            Debug.Log($"Find Chunk Count: {chunks.Count}");
            #endregion

            #region 메쉬 생성
            TerrainVertexInfo[,] data;
            for(int i = 0; i < chunks.Count; i++) {
                data = chunks[i];

                // 'TerrainVertexInfo'로 만들어지는 vertices의 인덱스값을 빠르게 찾기 위함
                int[,] vertIndexMap = new int[vertCountR, vertCountC];

                // 버텍스 배열을 먼저 생성
                List<Vector3> vertices = new List<Vector3>();
                int vertIndex = 0;
                for(int r = 0; r < vertCountR; r++) {
                    for(int c = 0; c < vertCountC; c++) {
                        if(data[r, c] != null) {
                            vertices.Add(data[r, c].Pos);

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

                Mesh mesh = new Mesh();
                mesh.vertices = vertices.ToArray();
                mesh.triangles = triangles.ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();

                GameObject go = new GameObject($"CutMesh_{i}");
                MeshFilter mf = go.AddComponent<MeshFilter>();
                mf.mesh = mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
            }
            #endregion
        }
        #endregion

        private class TerrainVertexInfo {
            public int KeyR => keyR;
            private int keyR = -1;

            public int KeyC => keyC;
            private int keyC = -1;

            public Vector3 Pos => pos;
            private Vector3 pos = Vector3.zero;



            public TerrainVertexInfo(int kR, int kC, Vector3 p) {
                keyR = kR;
                keyC = kC;
                pos = p;
            }
        }
    }
}