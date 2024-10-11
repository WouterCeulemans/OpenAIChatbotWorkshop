import { HubConnectionBuilder } from "@microsoft/signalr";
import { MessageUpdate } from "@models";

const connection = new HubConnectionBuilder()
    .withUrl("/chatHub")
    .build();

const messageInput = document.getElementById("message-input") as HTMLInputElement;
const sendBtn = document.getElementById("send-btn") as HTMLButtonElement;
const chatMessages = document.getElementById("chat-messages") as HTMLDivElement;

let currentThreadId: string | null = null;

sendBtn.addEventListener("click", () => {
    const message = messageInput.value;
    if (message) {
        connection.invoke("SendMessage", currentThreadId, message).then((threadId) => {
            if (threadId) {
                currentThreadId = threadId;
            }
        });
        appendMessage(message, "user");
        messageInput.value = '';
    }
});

function appendMessage(message: string, role: "user" | "assistant", messageId?: string): void {
    let messageDiv: HTMLDivElement | null;

    if (messageId) {
        messageDiv = document.getElementById(messageId) as HTMLDivElement;
        if (messageDiv) {
            messageDiv.textContent = message;
        }
    } else {
        messageDiv = document.createElement("div");
        messageDiv.id = `message-${crypto.randomUUID()}`;
        messageDiv.className = `message ${role}`;
        messageDiv.textContent = message;
        chatMessages.appendChild(messageDiv);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }
}

connection.on("ReceiveMessageUpdate", (messageUpdate: MessageUpdate) => {
    messageUpdate.createdOn = new Date(messageUpdate.createdOn);

    let existingMessageId = document.querySelector('#chat-messages .message.assistant:last-child')?.id;
    appendMessage(messageUpdate.text, messageUpdate.role, existingMessageId);
});

connection.start().catch(err => console.error(err));
