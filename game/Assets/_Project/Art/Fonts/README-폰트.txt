Galmuri11.ttf
=============

한국어 비트맵(도트) 폰트. 이 게임의 UI 전체가 이 폰트를 쓴다.

출처   : https://github.com/quiple/galmuri  (npm: galmuri)
버전   : 2.40.3 에서 받음 (2026-08-22)
라이선스: SIL Open Font License 1.1 (OFL-1.1)
         → 게임에 포함해 배포 가능. 폰트 자체를 판매하는 것만 금지.
         → OFL 전문을 배포물에 함께 넣어야 한다. 아직 안 받았다 🔴

🔴 왜 넣었나
   OnGUI는 유니티 기본 폰트를 쓰는데 거기엔 **한글 글리프가 없다.**
   에디터(Windows)에서는 시스템 폰트로 대체돼 잘 보이지만,
   WebGL 빌드에는 시스템 폰트가 없어서 **한글이 전부 빈칸**이 된다.
   2026-08-22 첫 itch 빌드에서 그대로 겪었다.

⚠️ 출시 전에 확인할 것
   패키지 설명에 "Bitmap fonts based on the font design from Nintendo DS"라고 적혀 있다.
   선언된 라이선스는 OFL-1.1이지만, **유료 배포(스팀) 전에 한 번 더 확인**할 것.
   대안: 둥근모꼴(DungGeunMo), Neo둥근모 등 다른 OFL 한글 도트 폰트.

⚠️ 용량
   5.3MB다. WebGL 빌드가 그만큼 커진다.
   줄이려면 실제로 쓰는 글자만 남기는 서브셋 폰트를 만들어야 한다 (fonttools).
