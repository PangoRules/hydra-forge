/**
 * LLM usage costs are computed from $/token pricing (often < $0.0001 per call for
 * cheap models), so a fixed 2-4 decimal display rounds real, nonzero cost to
 * "$0.0000" and reads as broken. Show as many decimals as needed to reveal the
 * actual value, without padding ordinary dollar amounts with noise.
 */
export function formatCost(value: number): string {
  if (value === 0) return '$0.00'
  return `$${value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 8 })}`
}
