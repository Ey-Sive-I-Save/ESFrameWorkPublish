using System;

namespace ES
{
    /// <summary>
    /// 单次渲染后端 Apply 会话。它只编排受控 Writer，不持有或直接修改 Unity 状态。
    /// </summary>
    public sealed class ESRenderBackendApplySession
    {
        private readonly ESRenderBackendChangePlan plan;
        private readonly ESRenderBackendApplyGate gate;
        private bool consumed;

        private ESRenderBackendApplySession(
            ESRenderBackendChangePlan plan,
            ESRenderBackendApplyGate gate)
        {
            this.plan = plan;
            this.gate = gate;
        }

        public bool IsConsumed => consumed;
        public bool IsAuthorized => gate.IsReady;

        public static bool TryCreate(
            ESRenderBackendChangePlan plan,
            ESRenderBackendApplyGate gate,
            string idempotencyKey,
            out ESRenderBackendApplySession session,
            out string reason)
        {
            if (!plan.IsDryRun)
            {
                session = null;
                reason = "dry-run-plan-required-for-apply-session";
                return false;
            }

            if (!gate.MatchesIdempotencyKey(idempotencyKey))
            {
                session = null;
                reason = "apply-gate-idempotency-key-mismatch";
                return false;
            }
            if (!gate.MatchesPlan(plan))
            {
                session = null;
                reason = "apply-gate-plan-mismatch";
                return false;
            }

            session = new ESRenderBackendApplySession(plan, gate);
            reason = string.Empty;
            return true;
        }

        public ESRenderBackendReceipt Execute(
            Func<bool> writer,
            Func<ESRenderBackendSnapshot> captureAfter)
        {
            if (consumed)
                return Failed("apply-session-already-consumed");
            consumed = true;

            if (writer == null || captureAfter == null)
                return Failed("writer-and-after-capture-are-required");

            bool writerReportedSuccess;
            try
            {
                writerReportedSuccess = writer();
            }
            catch (Exception exception)
            {
                return Failed("unity-writer-threw-" + exception.GetType().Name);
            }

            if (!writerReportedSuccess)
                return ESRenderBackendReceipt.EvaluateApply(
                    plan,
                    default(ESRenderBackendSnapshot),
                    false);

            try
            {
                return ESRenderBackendReceipt.EvaluateApply(plan, captureAfter(), true);
            }
            catch (Exception exception)
            {
                return Failed("after-snapshot-capture-threw-" + exception.GetType().Name);
            }
        }

        private static ESRenderBackendReceipt Failed(string reason)
        {
            return ESRenderBackendReceipt.CreateFailure(reason);
        }
    }
}
