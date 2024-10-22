export function formatDate(date: Date): string {
    return date.toLocaleString('default', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}