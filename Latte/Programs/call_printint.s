
.extern _printInt
.extern _printString
.extern _readString
.extern _readInt
.extern _error
.intel_syntax
.global _main
_main:
PUSH RBP
MOV RBP, RSP
SUB RSP, 8
MOV QWORD PTR [RBP-1], 0
MOV QWORD PTR [RBP-2], 0
MOV QWORD PTR [RBP-3], 0
MOV AL, [RBP-2]
MOV DIL, [RBP-3]
AND AL, DIL
SETNE [RBP-4]
MOV AL, [RBP-1]
MOV DIL, [RBP-4]
AND AL, DIL
JE l0
MOV RDI, 3
CALL _printInt
l0:
MOV RAX, 0
MOV RSP, RBP
POP RBP
RET
_trueWithSideEffect:
PUSH RBP
MOV RBP, RSP
SUB RSP, 8
MOV RDI, 1
CALL _printInt
MOV RAX, 1
MOV RSP, RBP
POP RBP
RET
_falseWithSideEffect:
PUSH RBP
MOV RBP, RSP
SUB RSP, 8
MOV RDI, 0
CALL _printInt
MOV RAX, 0
MOV RSP, RBP
POP RBP
RET
