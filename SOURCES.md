# 素材与版本依据

v2 新增的工作台界面、海克斯外围细线与微光、文字标注是本工具自行创作的可选设计，不是英雄联盟历史素材。选择「经典原版」即可关闭外围装饰；旧版轮盘核心图集与方向布局保持原样。

检索与资源核对日期：2026-10-15。实现版本固定为 **League of Legends 12.21**。

## 版本与交互

- [Riot 官方 12.22 更新说明](https://www.leagueoflegends.com/en-us/news/game-updates/patch-12-22-notes/)：宣布将战术轮盘从四种信号扩充到八种。这是选择 12.21 作为经典四向版基准的依据。
- [Riot Smart Ping 使用说明](https://support.riotgames.com/en-us/league-of-legends/gameplay/smart-ping)：Alt/Ctrl + 左键打开、拖动选方向、释放发送、右键取消。此网页现在显示新版八向轮盘，**未将网页中的新版图当作旧版素材**。
- [Riot 2017 年表情介绍](https://nexus.leagueoflegends.com/en-us/2017/08/dev-introducing-emotes/)：旧表情轮盘的五格背景说明；网页当前有地区跳转，实际几何以归档 UI 文件为准。

## 原始轮盘：12.21 历史游戏资源

下列文件由 CommunityDragon 保存和转换，原始内容属于 Riot Games。CommunityDragon 是社区维护的游戏数据归档，并非 Riot 官方分发服务。

- [ContextWheel_Atlas 原图](https://raw.communitydragon.org/12.21/game/assets/ux/menu/contextwheel_atlas.png)，保存在 `assets/original/contextwheel_atlas.png`。
- [四向信号轮盘原始 UI 布局](https://raw.communitydragon.org/12.21/game/gameplay/lolpingradial.uibase.bin.json)，保存在 `references/ping-layout.json`。
- [五格表情轮盘原始 UI 布局](https://raw.communitydragon.org/12.21/game/gameplay/lolemoteradial.uibase.bin.json)，保存在 `references/emote-layout.json`。
- [游戏字体说明](https://raw.communitydragon.org/12.21/game/ux/fonts.bin.json)：HUD_ContextRadial 颜色为 RGBA(250,250,205,255)，英语指定 BeaufortBold，简体中文指定 FZDHTK。英文加载[同版本 BeaufortForLoL-Bold.otf](https://raw.communitydragon.org/12.21/plugins/rcp-fe-lol-typekit/global/default/fonts/beaufort/beaufortforlol-bold.otf)；中文以 Windows 字体替代，未宣称字体逐像素相同。
- [12.21 中文字符串](https://raw.communitydragon.org/12.21/game/data/menu/fontconfig_zh_cn.txt.json)：实际标签为「危险」「正在路上」「协助我」「敌人不见了」。提取片段保存在 `references/chinese-labels.json`，不凭记忆自行命名。

`tools/fetch_assets.py` 按布局中的 `mRect`、`mTextureUV`、`mFlipX` 和 `mFlipY` 裁切原图，生成 `assets/wheel` 下的贴图。四向顺序、中心取消按钮、286×286 原始尺寸及 0.13 秒入场时间均取自历史布局。没有生成或重绘图标、边框和选中区域。

## 信号与表情

- [12.21 信号粒子贴图目录](https://raw.communitydragon.org/12.21/game/assets/shared/particles/)：使用其中 `ping-danger*`、`ping-omw*`、`ping-comehere*`、`ping-mia*`。游戏的加法混合贴图在桌面中转换成透明混合，并读取对应颜色渐变图取色。
- [12.21 表情目录（英文）](https://raw.communitydragon.org/12.21/plugins/rcp-be-lol-game-data/global/default/v1/summoner-emotes.json)。
- [12.21 表情目录（简体中文）](https://raw.communitydragon.org/12.21/plugins/rcp-be-lol-game-data/global/zh_cn/v1/summoner-emotes.json)。
- 表情 ID 为 1459、1492、1030、1445、1457、1499、1501、1505、1506、3153、3154、3155、3159、3166、3167，全部能在该版目录中找到。每一张图片 URL 和名称保存在 `assets/emotes/catalog.json`。

## 音效

音效来自公开项目 [darrenprx/lolPing](https://github.com/darrenprx/lolPing)，固定到提交 [c98806ae96a4dd83ca9c12be224878057f3d3ae7](https://github.com/darrenprx/lolPing/tree/c98806ae96a4dd83ca9c12be224878057f3d3ae7)。使用 `Danger.wav`、`OnMyWay.wav`、`ComeHere.wav`、`MIA.wav`、`Base.wav`、`button.wav`。该项目注明这些 WAV 提取自游戏，属于 Riot Games，**不能由此证明每个样本就是 12.21 版本**。

本程序的桌面代码独立编写，没有使用该项目的新版八向轮盘实现。保留其 [版权及授权说明](references/upstream-license.txt) 供追溯；其 MIT 授权不覆盖游戏美术与音效。

## 可以核实与尚未核实的边界

可核实：版本固定、原始图标与边框、美术裁切、轮盘布局、表情原图、资源 URL、文件大小及 SHA-256（`assets/manifest.json`）。

桌面适配：全局键盘鼠标操作、常驻轮盘与位置保存、置顶与鼠标穿透、独立音量、屏幕边缘修正。

未达到逐帧完全复刻：游戏地面投影及粒子系统、表情头顶世界坐标、每条动画曲线、游戏字体栅格化、12.21 音频库的逐样本匹配。程序用原贴图重放扩散和弹出效果，表情使用静态库存原图加弹出淡出，**不将这些适配描述成已经验证的一比一**。

## 权利归属

游戏名称、图标、表情、贴图、音效等内容归 Riot Games, Inc. 及相关权利人所有。[Riot 法律说明](https://www.riotgames.com/en/legal)。本程序不由 Riot Games 提供、赞助或认可。
