<?xml version="1.0" encoding="UTF-8"?>
<!-- FreeMind 1.0.1 / XMind「文件 → 导入 → FreeMind」可打开 -->
<map version="1.0.1">
  <node TEXT="Astra NVH 自动化测试平台 — 架构" FOLDED="false">
    <node TEXT="宿主" POSITION="right">
      <node TEXT="Astra.exe"/>
      <node TEXT="WPF 主程序壳、启动与全局 DI"/>
      <node TEXT="日志、导航框架、插件宿主入口"/>
    </node>
    <node TEXT="核心业务" POSITION="right">
      <node TEXT="Astra.Core">
        <node TEXT="工作流与节点模型"/>
        <node TEXT="设备抽象与配置"/>
        <node TEXT="触发器与联锁"/>
        <node TEXT="插件：发现、加载、宿主实现"/>
      </node>
      <node TEXT="Astra.Contract">
        <node TEXT="主程序与插件共用契约"/>
        <node TEXT="稳定 API 边界、便于演进"/>
      </node>
      <node TEXT="Astra.Engine">
        <node TEXT="工作流引擎 UI：View / ViewModel"/>
      </node>
      <node TEXT="Astra.Infrastructure">
        <node TEXT="EF Core + SQLite 持久化"/>
        <node TEXT="与 Core 解耦"/>
      </node>
    </node>
    <node TEXT="共享库" POSITION="left">
      <node TEXT="Astra.UI + Abstractions + NavStack">
        <node TEXT="共享控件与样式"/>
        <node TEXT="页面导航栈"/>
      </node>
      <node TEXT="NVHDataBridge">
        <node TEXT="TDMS / 音频等数据读写"/>
        <node TEXT="格式处理"/>
      </node>
    </node>
    <node TEXT="插件 Plugins" POSITION="left">
      <node TEXT="按 .addin 加载，输出到 Bin\...\Plugins\"/>
      <node TEXT="DataAcquisition / PLC / AudioPlayer"/>
      <node TEXT="WorkflowArchive / Limits 等"/>
    </node>
  </node>
</map>
