"""
build/webgl 폴더를 itch.io에 올릴 zip으로 묶는다.

🔴 왜 파이썬으로 만드는가 — Windows의 압축 도구들이 경로를 역슬래시로 쓴다.

   PowerShell `Compress-Archive`도, .NET `ZipFile.CreateFromDirectory`도
   Windows에서는 `Build\\webgl.loader.js` 처럼 역슬래시로 항목 이름을 넣는다.
   zip 규격은 '/'라서, itch 서버가 `Build` 폴더를 만들지 못하고
   그 안의 파일이 전부 404가 된다.

   결과: 루트의 index.html만 열려서 **로딩 화면은 뜨는데 게임은 안 뜬다.**
   2026-08-22에 그대로 겪었다 — 원인 찾는 데 한참 걸렸다.

사용법:  python tools/make-zip.py
"""

import os
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "build", "webgl")
OUT = os.path.join(ROOT, "build", "SalvageRun-webgl.zip")

REQUIRED = ["index.html"]


def main():
    if not os.path.isdir(SRC):
        print(f"[X] 빌드 폴더가 없다: {SRC}")
        print("    먼저 tools/unity-webgl.ps1 로 빌드할 것")
        return 1

    for name in REQUIRED:
        if not os.path.exists(os.path.join(SRC, name)):
            print(f"[X] {name} 이 없다. 빌드가 제대로 안 된 것 같다")
            return 1

    if os.path.exists(OUT):
        os.remove(OUT)

    count = 0
    with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for base, _dirs, files in os.walk(SRC):
            for f in files:
                full = os.path.join(base, f)
                # 🔴 여기가 핵심 — 항상 '/' 로 바꾼다
                arc = os.path.relpath(full, SRC).replace(os.sep, "/")
                z.write(full, arc)
                count += 1

    # 확인 — index.html 이 zip 루트에 있어야 itch 가 찾는다
    with zipfile.ZipFile(OUT) as z:
        names = z.namelist()

    if "index.html" not in names:
        print("[X] index.html 이 zip 루트에 없다. itch 가 못 찾는다")
        return 1

    bad = [n for n in names if "\\" in n]
    if bad:
        print(f"[X] 역슬래시가 섞였다: {bad[:3]}")
        return 1

    size = os.path.getsize(OUT) / 1024 / 1024
    print(f"[*] zip 완료 · 파일 {count}개 · {size:.1f} MB")
    print(f"    {OUT}")
    for n in names:
        print(f"      {n}")
    print()
    print("  itch.io 업로드:")
    print("   1. 파일 업로드 > 이 zip")
    print("   2. 올라간 파일 옆 '이 파일은 브라우저에서 플레이됨' 체크")
    print("   3. 뷰포트 960 x 600 · 전체화면 버튼 켜기 · 스크롤바 끄기")
    print("   4. 공개 & 접근: 초안")
    return 0


if __name__ == "__main__":
    sys.exit(main())
