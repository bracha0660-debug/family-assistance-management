import { useCallback, useEffect, useRef, useState, type ChangeEvent } from 'react';
import type { UserDto } from '../../api/auth';
import type { CommitteeDecisionDto } from '../../api/committeeDecisions';
import {
  executePayment,
  markPaymentPaid,
  returnPaymentToCoordinator,
  uploadPaymentProof,
  type PaymentQueueItemDto,
} from '../../api/payments';
import { getWorkflowDashboard, type WorkflowDashboardResponse } from '../../api/workflow';
import { ApprovalActionModal, type ApprovalAction } from './ApprovalActionModal';
import { AwaitingMyActionSummaryPanel } from './AwaitingMyActionSummary';
import { WorkflowSectionPanel } from './WorkflowSectionPanel';
import { SECTION_ORDER, canCreateRequest } from './workflowSections';
import { CommitteeDecisionsPage } from '../CommitteeDecisionsPage';

interface WorkflowDashboardPageProps {
  user: UserDto;
}

export function WorkflowDashboardPage({ user }: WorkflowDashboardPageProps) {
  const [data, setData] = useState<WorkflowDashboardResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [approvalModal, setApprovalModal] = useState<{
    decision: CommitteeDecisionDto;
    action: ApprovalAction;
  } | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [uploadTarget, setUploadTarget] = useState<PaymentQueueItemDto | null>(null);

  const load = useCallback(async () => {
    setError('');
    try {
      setData(await getWorkflowDashboard());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  function scrollToSection(sectionId: string) {
    document.getElementById(`section-${sectionId}`)?.scrollIntoView({ behavior: 'smooth' });
  }

  function handleDecisionAction(decision: CommitteeDecisionDto, action: string) {
    if (action === 'edit' || action === 'submit' || action === 'cancel') {
      setShowCreate(true);
      return;
    }
    if (['approve', 'reject', 'return', 'suspend', 'resume'].includes(action)) {
      setApprovalModal({ decision, action: action as ApprovalAction });
    }
  }

  async function runPaymentAction(id: string, fn: () => Promise<void>) {
    setActionLoading(id);
    setError('');
    try {
      await fn();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setActionLoading(null);
    }
  }

  function handlePaymentAction(payment: PaymentQueueItemDto, action: string) {
    if (action === 'execute') {
      void runPaymentAction(payment.id, async () => {
        await executePayment(payment.id, payment.version);
      });
      return;
    }
    if (action === 'upload_proof') {
      setUploadTarget(payment);
      fileInputRef.current?.click();
      return;
    }
    if (action === 'mark_paid') {
      void runPaymentAction(payment.id, async () => {
        await markPaymentPaid(payment.id, payment.version);
      });
      return;
    }
    if (action === 'return_to_coordinator') {
      const reason = window.prompt('סיבת החזרה לרכז (3–500 תווים):');
      if (!reason || reason.trim().length < 3) return;
      void runPaymentAction(payment.id, async () => {
        await returnPaymentToCoordinator(payment.id, payment.version, reason.trim());
      });
    }
  }

  async function handleFileSelected(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file || !uploadTarget) return;
    await runPaymentAction(uploadTarget.id, async () => {
      await uploadPaymentProof(uploadTarget.id, uploadTarget.version, file);
    });
    setUploadTarget(null);
  }

  const orderedSections = data?.sections
    .slice()
    .sort((a, b) => {
      const ai = SECTION_ORDER.indexOf(a.sectionId);
      const bi = SECTION_ORDER.indexOf(b.sectionId);
      return (ai === -1 ? 999 : ai) - (bi === -1 ? 999 : bi);
    }) ?? [];

  if (showCreate) {
    return (
      <div>
        <button type="button" className="btn-secondary" onClick={() => setShowCreate(false)}>
          חזרה ללוח בקרה
        </button>
        <CommitteeDecisionsPage user={user} />
      </div>
    );
  }

  return (
    <div className="workflow-dashboard workflow-dashboard-page">
      <div className="workflow-dashboard-toolbar">
        {canCreateRequest(user) && (
          <button type="button" className="workflow-btn-primary" onClick={() => setShowCreate(true)}>
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
              <path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z" />
            </svg>
            בקשה חדשה
          </button>
        )}
      </div>

      <input
        ref={fileInputRef}
        type="file"
        accept=".pdf,.jpg,.jpeg,.png"
        hidden
        onChange={handleFileSelected}
      />

      {loading && <p className="workflow-loading">טוען...</p>}
      {error && <p className="error" role="alert">{error}</p>}

      {data && (
        <>
          <AwaitingMyActionSummaryPanel
            summary={data.awaitingMyAction}
            onSectionClick={scrollToSection}
          />

          {orderedSections.map((section) => (
            <WorkflowSectionPanel
              key={section.sectionId}
              section={section}
              defaultExpanded={section.awaitingActionCount > 0}
              onDecisionAction={handleDecisionAction}
              onPaymentAction={handlePaymentAction}
            />
          ))}
        </>
      )}

      {actionLoading && <p className="loading-hint">מבצע פעולה...</p>}

      {approvalModal && (
        <ApprovalActionModal
          decision={approvalModal.decision}
          action={approvalModal.action}
          onClose={() => setApprovalModal(null)}
          onCompleted={() => load()}
        />
      )}
    </div>
  );
}
