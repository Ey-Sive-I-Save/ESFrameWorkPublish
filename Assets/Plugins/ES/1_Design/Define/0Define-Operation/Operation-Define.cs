using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
        /*什么是Operation
          操作是对一个规范执行事件的逻辑对象的总称，在他下有几类分支
           @ 常规IOperation,默认操作，传入作用和使用的信息完成任务，
           一般直接使用绕开OutputOperation的标准
           @OutputOperation 可输出操作，作为看起来最简单的接口，
          ！在旧标准中实现了针的 On From With 的关系方法，可以不借助额外参数执行命令
          ！！在新标准中只需要IRuntimeTarget和IRuntimeLogic
          @TargetOperation 导向操作 ,他可以把一个值修改的目标操作直接导向目的地，通常作为一个拼接辅助扩展

          */
        public interface IOperation
        {

        }

        public interface IOperation<On,From,With> : IOperation
        {

        }

        public interface IOperation<Target,With> : IOperation
        {

        }
    
}
