"use client";

import { useChat } from "@ai-sdk/react";
import { type AiSdkChat, createChat } from "@shadcn/helpers/ai-sdk";
import { Notice } from "@teck/ui/blocks/notice";
import { Button } from "@teck/ui/components/button";
import {
  lastAssistantMessageIsCompleteWithApprovalResponses,
  type UIDataTypes,
  type UIMessage,
} from "ai";
import { useMemo, useState } from "react";

export type OfflineChatScenario = "timed" | "error" | "approval";

type PreviewTool = {
  input: { sku: string };
  output: { preview: string };
};

type FixtureTools = {
  showPreview: PreviewTool;
};

export type FixtureUIMessage = UIMessage<unknown, UIDataTypes, FixtureTools>;

/**
 * Long enough for a test to observe the first text part before the transport
 * delivers the second one, without relying on network or wall-clock guessing
 * beyond this explicit scripted pause.
 */
const PARTIAL_DELAY_MS = 400;

/** Illustrative in-memory commerce preview. The tool never calls HTTP or mutates state. */
const MOCK_PREVIEW_OUTPUT = {
  preview: "Draft order #1042: 2 add-ons",
};

function createFixtureChat(scenario: OfflineChatScenario): AiSdkChat<FixtureUIMessage> {
  const chat = createChat<FixtureUIMessage>({ now: new Date(0) });

  if (scenario === "timed") {
    return chat.user("Show the offline preview timeline.").assistant(({ writer }) => {
      writer.text("Partial fixture response", {
        mode: "instant",
        delayMs: 0,
      });
      writer.sleep(PARTIAL_DELAY_MS);
      writer.text(" completed after the pause.", {
        mode: "instant",
        delayMs: 0,
      });
    });
  }

  if (scenario === "error") {
    return chat.user("Show the offline error handling.").assistant(({ writer }) => {
      writer.text("Partial before error", {
        mode: "instant",
        delayMs: 0,
      });
      writer.sleep(PARTIAL_DELAY_MS);
      writer.error("Fixture stream failed");
      writer.text("Final text must not appear", {
        mode: "instant",
        delayMs: 0,
      });
    });
  }

  return chat
    .user("Preview the draft order.")
    .assistant(({ writer }) => {
      writer.text("This preview needs your approval.", {
        mode: "instant",
        delayMs: 0,
      });
      writer.tool("showPreview", {
        input: { sku: "DRAFT-1042" },
        output: MOCK_PREVIEW_OUTPUT,
        needsApproval: true,
      });
    })
    .assistant(({ writer, toolCall }) => {
      writer.text(toolCall?.approved ? "Preview approved." : "Preview denied.", {
        mode: "instant",
        delayMs: 0,
      });
    });
}

export function OfflineChatFixture({ scenario = "timed" }: { scenario?: OfflineChatScenario }) {
  const [chat] = useState(() => createFixtureChat(scenario));
  const initialMessages = useMemo(() => chat.get(0), [chat]);
  const transport = useMemo(() => chat.transport({ delayMs: 0 }), [chat]);

  const { messages, sendMessage, status, error, addToolApprovalResponse } =
    useChat<FixtureUIMessage>({
      id: `offline-chat-${scenario}`,
      messages: initialMessages,
      transport,
      sendAutomaticallyWhen: lastAssistantMessageIsCompleteWithApprovalResponses,
    });

  const start = () => {
    const next = chat.next(messages);

    if (next !== null) {
      void sendMessage(next);
    }
  };

  return (
    <section aria-label="Offline chat fixture" data-testid="offline-chat-fixture">
      <h2>Offline chat fixture</h2>
      <Button
        type="button"
        onClick={start}
        disabled={status === "submitted" || status === "streaming"}
      >
        Send fixture message
      </Button>
      <p data-testid="chat-status">{status}</p>
      <ol>
        {messages.map((message) => (
          <li key={message.id} data-role={message.role}>
            {message.parts.map((part, index) => {
              const key = `${message.id}-${index}`;

              if (part.type === "text") {
                return <p key={key}>{part.text}</p>;
              }

              if (part.type !== "tool-showPreview") {
                return null;
              }

              if (part.state === "approval-requested") {
                const approvalId = part.approval.id;

                return (
                  <div key={key} data-testid="approval-request">
                    <p>Approve the preview for {part.input.sku}?</p>
                    <Button
                      type="button"
                      onClick={() =>
                        void addToolApprovalResponse({
                          id: approvalId,
                          approved: true,
                        })
                      }
                    >
                      Approve
                    </Button>
                    <Button
                      type="button"
                      onClick={() =>
                        void addToolApprovalResponse({
                          id: approvalId,
                          approved: false,
                        })
                      }
                    >
                      Deny
                    </Button>
                  </div>
                );
              }

              if (part.state === "output-available") {
                return (
                  <p key={key} data-testid="tool-output">
                    {part.output.preview}
                  </p>
                );
              }

              if (part.state === "output-denied") {
                return (
                  <p key={key} data-testid="tool-denied">
                    Preview denied; no output was produced.
                  </p>
                );
              }

              return null;
            })}
          </li>
        ))}
      </ol>
      {error === undefined ? null : <Notice tone="error">{error.message}</Notice>}
    </section>
  );
}
