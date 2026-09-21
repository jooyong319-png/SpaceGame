using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SalvageRun.Orbit;

namespace SalvageRun.EditorTools
{
    /// <summary>
    /// 궤도 청소부 도트를 한 장의 견본으로 굽는다 — 게임을 안 돌려도 그림을 한눈에 판정한다.
    /// MCP 에서 한 줄로 부른다: <c>SalvageRun.EditorTools.OrbitArtSheet.Bake("경로.png")</c>
    /// (MCP 명령 안에서는 파일 쓰기가 막혀 있어서 프로젝트 쪽에 둔다)
    /// </summary>
    public static class OrbitArtSheet
    {
        public static string Bake(string path)
        {
            var rows = new List<Sprite[]>
            {
                new[] { OrbitArt.Drone(), OrbitArt.FriendlySat(), OrbitArt.DeadSat(), OrbitArt.BigWreck(), OrbitArt.Station() },
                OrbitArt.Debris(),
                new[] { OrbitArt.Structure("yard"), OrbitArt.Structure("scanner"), OrbitArt.Structure("board"), OrbitArt.Structure("salvager"), OrbitArt.Structure("recycle") },
                new[] { OrbitArt.Structure("grade2"), OrbitArt.Structure("swarm"), OrbitArt.Structure("refinery"), OrbitArt.Structure("cap") },
            };
            const int S = 6, pad = 10, cell = 44 * S;
            int W = pad + 6 * (cell + pad), H = pad + rows.Count * (cell + pad);
            var outTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var bg = new Color32(9, 11, 16, 255);
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    var sp = rows[r][c];
                    var src = sp.texture.GetPixels32();
                    int sw = sp.texture.width, sh = sp.texture.height;
                    int ox = pad + c * (cell + pad) + (cell - sw * S) / 2;
                    int oy = H - (pad + (r + 1) * (cell + pad)) + pad + (cell - sh * S) / 2;
                    for (int y = 0; y < sh * S; y++)
                        for (int x = 0; x < sw * S; x++)
                        {
                            var p = src[(y / S) * sw + (x / S)];
                            if (p.a == 0) continue;
                            int X = ox + x, Y = oy + y;
                            if (X < 0 || Y < 0 || X >= W || Y >= H) continue;
                            px[Y * W + X] = p;
                        }
                }
            outTex.SetPixels32(px);
            outTex.Apply();
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
            return path;
        }
    }
}
