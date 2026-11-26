export type ConflictSnapshot = {
  revision: number;
  status: string;
  approvalStatus: string;
};

export type MutationState =
  | { status: "idle" }
  | { status: "success"; message: string }
  | {
      status: "conflict";
      message: string;
      current: ConflictSnapshot | null;
    }
  | { status: "error"; message: string }
  | { status: "unauthorized" }
  | { status: "forbidden" }
  | { status: "unavailable" };

export type MutationAction = (state: MutationState, formData: FormData) => Promise<MutationState>;

export const idleMutationState: MutationState = { status: "idle" };
