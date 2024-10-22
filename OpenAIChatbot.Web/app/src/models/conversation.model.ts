export interface Conversation {
    id: string;
    threadId: string;
    assistantId: string;
    createdOn: Date;
    title: string | undefined | null;
}