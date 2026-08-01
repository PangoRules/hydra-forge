/**
 * Shared composable for resolving a userId to a display name via the board
 * store's member list. Used by CardSpec and CardPlan version history.
 */
export function useMemberDisplay() {
  const board = useBoardStore()

  function shortUser(userId: string): string {
    return board.members.find(m => m.userId === userId)?.username ?? userId.slice(0, 8) + '...'
  }

  return { shortUser }
}
