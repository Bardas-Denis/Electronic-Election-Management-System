// SYNC: LabelDtos.cs -> LabelDto, UserLabelDto, UserWithLabelDto, CreateLabelRequest, AssignLabelsRequest

export interface Label {
  id: string;
  name: string;
  category?: string | null;
  createdAt: string;
}

/** Label assignment as seen per-user (includes assignment metadata). */
export interface UserLabel {
  labelId: string;
  name: string;
  category?: string | null;
  assignedBy: string;
  assignedAt: string;
}

/** A user record returned when querying who has a specific label. */
export interface UserWithLabel {
  userId: string;
  email: string;
  assignedAt: string;
}

export interface CreateLabelRequest {
  name: string;
  category?: string | null;
}

export interface AssignLabelsRequest {
  labelIds: string[];
}
