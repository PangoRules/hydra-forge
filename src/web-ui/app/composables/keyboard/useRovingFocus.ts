export function useRovingFocus(ids: () => string[]) {
  const selectedIndex = ref(0)

  const selectedId = computed(() => ids()[selectedIndex.value] ?? null)

  function next() {
    const length = ids().length
    if (length === 0) return
    selectedIndex.value = (selectedIndex.value + 1) % length
  }

  function prev() {
    const length = ids().length
    if (length === 0) return
    selectedIndex.value = (selectedIndex.value - 1 + length) % length
  }

  function select(index: number) {
    selectedIndex.value = index
  }

  function selectById(id: string) {
    const index = ids().indexOf(id)
    if (index !== -1) selectedIndex.value = index
  }

  function isSelected(index: number): boolean {
    return index === selectedIndex.value
  }

  return { selectedIndex, selectedId, next, prev, select, selectById, isSelected }
}
