using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//只是一种定义 用来规范架构 师出有名 没有实际逻辑

/*
 * Environment 环境 非常简单 很可能是一个枚举或者静态类
 * 他规定了 同一个事件 因为 不同环境下的不同执行方案
 * 可以用来当做 动态策略 的 键
 * 比如一个事件信息(Link),因为在客户端，他可能就会直接悄悄执行，
 * 如果在服务器端，可能会发送到全部客户端 这些可以被预先定义来简化
 * 也可能是一个怪物，在不同的地形下 使用不同的策略来攻击玩家
 * 如果是是全局的 各种 Current-Envionment (放在GameCenterManager里哈)
 */

public enum Environment_Example_Languguage
{
    [InspectorName("中文的")] Chinise,
    [InspectorName("英文的")] English,
    [InspectorName("日本的")] Japan,
    [InspectorName("依薇尔的")] Everey,
}


