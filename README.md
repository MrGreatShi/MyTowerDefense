# MyTowerDefense

Unity 塔防游戏项目。

## 打开项目

1. 安装 Unity **2022.3.52f1c1**（版本以 `ProjectSettings/ProjectVersion.txt` 为准）。
2. 克隆仓库：`git clone https://github.com/MrGreatShi/MyTowerDefense.git`
3. 在 Unity Hub 中添加克隆后的项目目录并打开，等待资源导入及 Packages 恢复。

## 版本管理

`Assets/`（包含 `.meta`）、`Packages/` 和 `ProjectSettings/` 都纳入 Git。
Unity 缓存、临时文件、构建输出及本地 IDE 配置由 `.gitignore` 排除。
移动或重命名资源时请同时保留对应 `.meta`，建议在 Unity 编辑器中操作。

日常保存修改：

```sh
git add -A
git commit -m "Describe the changes"
git push
```

多人协作时，开始工作前运行 `git pull --ff-only`，并尽量避免同时编辑同一场景或预制体。
