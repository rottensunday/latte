
.extern _printInt
.extern _printString
.extern _readString
.extern _readInt
.extern _error
.extern _concatStrings
.extern _compareStrings
.intel_syntax
.global _main
_main:
PUSH RBP
MOV RBP, RSP
SUB RSP, 72
CALL _readInt
MOV [RBP-8], RAX
MOV RAX, [RBP-8]
MOV [RBP-16], RAX
CALL _readString
MOV [RBP-24], RAX
MOV RAX, [RBP-24]
MOV [RBP-32], RAX
CALL _readString
MOV [RBP-40], RAX
MOV RAX, [RBP-40]
MOV [RBP-48], RAX
MOV RAX, [RBP-16]
SUB RAX, 5
MOV [RBP-56], RAX
MOV RAX, [RBP-56]
MOV RDI, RAX
CALL _printInt
MOV RDI, [RBP-32]
MOV RSI, [RBP-48]
CALL _concatStrings
MOV [RBP-64], RAX
MOV RAX, [RBP-64]
MOV RDI, RAX
CALL _printString
MOV RAX, 0
MOV RSP, RBP
POP RBP
RET
