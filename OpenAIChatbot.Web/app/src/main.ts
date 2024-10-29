import { HubConnectionBuilder } from "@microsoft/signalr";
import { Conversation, Message, MessageUpdate } from "@models";
import { formatAsMarkdown } from "./message-formatter";
import { formatDate } from "./utils";

const connection = new HubConnectionBuilder()
    .withUrl("/chatHub")
    .build();

const messageInput = document.getElementById("message-input") as HTMLInputElement;
const sendBtn = document.getElementById("send-btn") as HTMLButtonElement;
const chatMessages = document.getElementById("chat-messages") as HTMLDivElement;
const newConversationBtn = document.getElementById("new-conversation-btn") as HTMLButtonElement;
const conversationHistory = document.getElementById("conversation-history") as HTMLDivElement;
const fileInput = document.getElementById("file") as HTMLInputElement;
const fileTags = document.getElementById("file-tags") as HTMLDivElement;

let currentConversationId: string | null = null;
let cachedConversations: Conversation[] = [];
let uploadedFiles: { [key: string]: string } = {};

sendBtn.addEventListener("click", () => {
    const message = messageInput.value;
    if (message) {
        const fileIds = Object.values(uploadedFiles);
        connection.invoke("SendMessage", currentConversationId, message, fileIds).then((conversation: Conversation) => {
            if (conversation) {
                currentConversationId = conversation.id;
                conversation.createdOn = new Date(conversation.createdOn);
                if (!cachedConversations.some(x => x.id == conversation.id)) {
                    cachedConversations.unshift(conversation);
                    renderConversations();
                }
            }
        });
        appendMessage(message, "user");
        messageInput.value = '';
        uploadedFiles = {};
        renderFiles();
    }
});

newConversationBtn.addEventListener("click", () => {
    currentConversationId = null;
    chatMessages.innerHTML = '';
    clearSelectedConversation();
});

fileInput.addEventListener("change", async () => {
    if (!fileInput.files || fileInput.files.length == 0) {
        return;
    }

    const formData = new FormData();
    for (let i = 0; i < fileInput.files.length; i++) {
        formData.append('files', fileInput.files[i]);
    }

    let uploadResponse = await fetch('/api/files/upload', {
        method: 'POST',
        body: formData
    });
    let results = await uploadResponse.json() as { [key: string]: string };
    Object.entries(results).forEach(([key, value]) => {
        uploadedFiles[key] = value;
    });

    renderFiles();
});

function appendMessage(message: string, role: "user" | "assistant", messageId?: string): void {
    let messageDiv: HTMLDivElement | null;

    if (messageId) {
        messageDiv = document.getElementById(messageId) as HTMLDivElement;
        if (messageDiv) {
            messageDiv.innerHTML = formatAsMarkdown(message);
        }
    } else {
        messageDiv = document.createElement("div");
        messageDiv.id = `message-${crypto.randomUUID()}`;
        messageDiv.className = `message ${role}`;
        messageDiv.innerHTML = formatAsMarkdown(message);
        chatMessages.appendChild(messageDiv);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }
}

function loadConversation(conversationId: string) {
    currentConversationId = conversationId;
    chatMessages.innerHTML = '';
    connection.invoke("GetConversationMessages", conversationId).then((messages: Message[]) => {
        messages.forEach((msg) => {
            appendMessage(msg.text, msg.role);
        });
    });
}

function clearSelectedConversation() {
    conversationHistory.querySelectorAll('.list-item').forEach(x => x.classList.remove('is-selected'));
}

function deleteConversation(conversationId: string) {
    connection.invoke("DeleteConversation", conversationId).then((success: boolean) => {
        if (success) {
            cachedConversations = cachedConversations.filter(x => x.id != conversationId);
            renderConversations();
            if (currentConversationId === conversationId) {
                currentConversationId = null;
                chatMessages.innerHTML = '';
            }
        }
    });
}

function renderConversations() {
    conversationHistory.innerHTML = cachedConversations.map(conversation => /*html*/`
        <div class="list-item ${conversation.id === currentConversationId ? "is-selected" : ""}" data-conversation-id="${conversation.id}">                            
            <div class="list-item-content">
                <div class="list-item-title">${conversation.title || conversation.id}</div>
                <div class="list-item-description">${formatDate(conversation.createdOn)}</div>
            </div>
            <div class="list-item-controls">
                <div class="buttons is-right">
                    <button class="button is-danger" data-delete-conversation-btn>
                        <span class="icon is-small">
                            <i class="fa-solid fa-trash"></i>
                        </span>
                    </button>                                                                        
                </div>
            </div>
        </div>
    `).join('')
}

function renderFiles() {
    fileTags.innerHTML = Object.keys(uploadedFiles).map(fileName => /*html*/`
        <span class="tag is-dark">${fileName}</span>
    `).join('');
}

connection.on("ReceiveMessageUpdate", (messageUpdate: MessageUpdate) => {
    messageUpdate.createdOn = new Date(messageUpdate.createdOn);

    let existingMessageId = document.querySelector('#chat-messages .message.assistant:last-child')?.id;
    appendMessage(messageUpdate.text, messageUpdate.role, existingMessageId);
});

connection.start()
    .then(() => {
        connection.invoke("GetConversations").then((conversations: Conversation[]) => {
            conversations.forEach(x => x.createdOn = new Date(x.createdOn))
            cachedConversations = conversations;
            renderConversations();
        });
    })
    .catch(err => console.error(err));

document.addEventListener('click', (e) => {
    if (e.target == null || !(e.target instanceof Element)) {
        return;
    }

    const deleteBtn = e.target.closest('[data-delete-conversation-btn]');
    if (deleteBtn) {
        const conversationId = deleteBtn.closest('[data-conversation-id]')?.getAttribute('data-conversation-id');
        if (conversationId) {
            deleteConversation(conversationId);
        }

        return;
    }

    const conversationElement = e.target.closest('[data-conversation-id]');
    if (conversationElement) {
        const conversationId = conversationElement.getAttribute('data-conversation-id');
        if (conversationId) {
            clearSelectedConversation();
            conversationElement.classList.add('is-selected');
            loadConversation(conversationId);
        }
    }
});