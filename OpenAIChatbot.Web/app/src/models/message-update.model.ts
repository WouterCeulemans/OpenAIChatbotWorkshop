export interface MessageUpdate {
    id: string;
    role: 'assistant' | 'user',
    text: string;
    createdOn: Date;
}