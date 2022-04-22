using System;
using System.Threading;
using JetBrains.Annotations;


namespace CPU7Plus.Emulation {
    
    /**
    *  This logic is more or less a direct port of Chris Giorgi's centemu.c emulator.
     *
     * One of these days I will get around to running the actual C code via a DLL or something to that effect.
    */
    public class EmulationCore {

        private EmulationContext _context;

        private const bool Debug = false;
        
        public EmulationCore(EmulationContext context) {
            _context = context;
        }

        /**
         * Singles steps the processor
         */
        public bool Step() {
            int pc = Context.Pc;

            byte isr = Context.Fetch8(pc);

            // Misc Class Instruction
            if ((isr & 0xF0) == 0x00) {

                switch (isr & 0x0F) {
                    case 0: // HLT
                       //Context.Pc = ToUShort(pc + 1);
                        return false;
                    
                    case 1: // NOP
                        break;
                    
                    case 2: // FSN
                        _context.FlagM = true;
                        break;
                    
                    case 3: // FCN
                        _context.FlagM = false;
                        break;
                    
                    case 4: // FSI
                        _context.FlagI = true;
                        break;
                    
                    case 5: // FCI
                        _context.FlagI = false;
                        break;
                    
                    case 6: // FSC
                        _context.FlagC = true;
                        break;
                    
                    case 7: // FCC
                        _context.FlagC = false;
                        break;
                    
                    case 8: // FCA
                        _context.FlagC = true;
                        _context.FlagF = false;
                        _context.FlagI = false;
                        _context.FlagL = false;
                        _context.FlagM = false;
                        _context.FlagV = false;
                        _context.FlagZ = false;
                        break;
                    
                    case 10: // RETI
                    case 9: // RET
                        _context.FlagL = false;
                        _context.Pc = _context.GetRegister16(2);
                        
                        // Pop off the top of stack
                        _context.SetRegister16(2, Context.Fetch16(Context.GetRegister16(5), false));
                        _context.SetRegister16(5, ToUShort(_context.GetRegister16(5) + 2));
                        return true;
                    
                    case 14: // DLY
                        Thread.Sleep(5);
                        break;

                }
                
                Context.Pc = ToUShort(pc + 1); 
            } 
            
            // Branch Class Instruction
            else if ((isr & 0xF0) == 0x10) {
                int offset = ToSignedOffset(Context.Fetch8(pc + 1));

                bool branch = false;
                bool pos = _context.FlagM ^ _context.FlagV;

                switch (isr & 0x0F) {
                    case 0: branch = _context.FlagC; break; // BCS
                    case 1: branch = !_context.FlagC; break; // BCC
                    case 2: branch = _context.FlagM; break; // BMS
                    case 3: branch = !_context.FlagM; break; // BMC
                    case 4: branch = _context.FlagZ; break; // BZS
                    case 5: branch = !_context.FlagZ; break; // BZC
                    case 6: branch = !pos && !_context.FlagZ; break; // BLT
                    case 7: branch = pos; break; // BLE
                    case 8: branch = pos && !_context.FlagZ; break; // BGT
                    case 9: branch = !pos || _context.FlagZ; break; // BGE
                    case 10: branch = Context.GetSenseSwitch(0); break; // BS1
                    case 11: branch = Context.GetSenseSwitch(0); break; // BS2
                    case 12: branch = Context.GetSenseSwitch(0); break; // BS3
                    case 13: branch = Context.GetSenseSwitch(0); break; // BS4
                }

                if (!branch) offset = 0;
                
                Context.Pc = ToUShort(pc + 2 + offset);
            }

            // Byte-Wise Single Operand Operations
            else if ((isr & 0xF0) == 0x20) {
                int length = 1;
                int reg;
                bool singlet = false;
                
                // Get register
                if ((isr & 0x08) == 0x08) {
                    reg = 1;
                    singlet = true;
                } else {
                    reg = Context.Fetch8(pc + 1) >> 4;
                    length = 2;
                }

                // Get inputs, outputs, and operation
                int inv = Context.GetRegister8(reg);
                int outv = 0;
                int op = isr & 0x07;

                if (op == 0 || op == 1) {
                    // INC / DEC
                    if (op == 0) outv = inv + 1;
                    else outv = inv - 1;

                    // Overflow check
                    Context.FlagV = (outv < 0) || (outv > 255);
                } else if (op == 2) {
                    // CLR
                    outv = 0;
                } else if (op == 3) {
                    // NOT
                    outv = ~inv;
                } else if (op == 4) {
                    // SRL
                    outv = inv >> 1;
                    Context.FlagC = (inv & 0x01) == 0x01;
                } else if (op == 5) {
                    // SLL
                    outv = inv << 1;
                    Context.FlagC = (inv & 0x80) == 0x80;
                } else if (op == 6 && !singlet) {
                    // RCC
                    outv = inv >> 1  | (Context.FlagC ? 0x80 : 0);
                    Context.FlagC = (inv & 0x01) == 0x01;
                } else if (op == 7 && !singlet) {
                    // RLC
                    outv = (inv << 1) | (Context.FlagC ? 0x01 : 0);
                    Context.FlagC = (inv & 0x80) == 0x80;
                }

                if (singlet && op == 6) {
                    // MMU
                } else if (singlet && op == 7) {
                    // DMA
                }

                //Console.Write("Op: " + op + " In: " + inv + " Out: " + outv + "\n");
                
                byte outb = ToByte(outv);
                
                // Set additional flags
                Context.FlagZ = outb == 0;
                Context.FlagM = (outb & 0x80) == 0x80; 
                
                // Write register and advance
                Context.SetRegister8(reg, outb);
                Context.Pc = ToUShort(pc + length);
            }
            // Word-Wise Single Operand Operations
            else if ((isr & 0xF0) == 0x30) {
                int length = 1;
                int reg;
                bool singlet = false;
                
                // Get register
                if ((isr & 0x08) == 0x08) {
                    reg = 0;
                    singlet = true;
                } else {
                    reg = Context.Fetch8(pc + 1) >> 5;
                    length = 2;
                }

                int outv = 0;
                int op = isr & 0x07;
                
                // Special operations
                if (singlet && op == 6) {
                    // INC RT
                    reg = 2;
                    op = 0;
                } else if (singlet && op == 7) {
                    // DEC RT
                    reg = 2;
                    op = 1;
                }

                // Get inputs, outputs, and operation
                int inv = Context.GetRegister16(reg);
                
                if (op == 0 || op == 1) {
                    // INC / DEC
                    if (op == 0) outv = inv + 1;
                    else outv = inv - 1;

                    // Overflow check
                    Context.FlagV = (outv < 0) || (outv > 65535);
                } else if (op == 2) {
                    // CLR
                    outv = 0;
                } else if (op == 3) {
                    // NOT
                    outv = ~inv;
                } else if (op == 4) {
                    // SRL
                    outv = inv >> 1;
                    Context.FlagC = (inv & 0x0001) == 0x0001;
                } else if (op == 5) {
                    // SLL
                    outv = inv << 1;
                    Context.FlagC = (inv & 0x8000) == 0x8000;
                } else if (op == 6 && !singlet) {
                    // RCC
                    outv = inv >> 1  | (Context.FlagC ? 0x8000 : 0);
                    Context.FlagC = (inv & 0x0001) == 0x0001;
                } else if (op == 7 && !singlet) {
                    // RLC
                    outv = (inv << 1) | (Context.FlagC ? 0x0001 : 0);
                    Context.FlagC = (inv & 0x8000) == 0x8000;
                }

                ushort outs = ToUShort(outv);
                
                // Set additional flags
                Context.FlagZ = outs == 0;
                Context.FlagM = (outs & 0x8000) == 0x8000; 
                
                // Write register and advance
                Context.SetRegister16(reg, outs);
                Context.Pc = ToUShort(pc + length);
            }
            
            // Byte-Wise Double Operand Operation
            else if ((isr & 0xF0) == 0x40) {
                int length = 1;
                int regDst;
                int regSrc;

                // Get register
                if ((isr & 0x08) == 0x08) {
                    regDst = 3;
                    regSrc = 1;
                } else {
                    regSrc = Context.Fetch8(pc + 1) >> 4;
                    regDst = Context.Fetch8(pc + 1) & 0x0F;
                    length = 2;
                }
                
                // Get inputs, outputs, and operation
                int inDst = Context.GetRegister8(regDst);
                int inSrc = Context.GetRegister8(regSrc);
                int outv = 0;
                int op = isr & 0x07;

                if (op == 0) {
                    // ADD
                    outv = inDst + inSrc;

                    Context.FlagC = (outv > 255);
                    Context.FlagV = ((ToSignedOffset(ToByte(inDst)) >= 0) == (ToSignedOffset(ToByte(inSrc)) >= 0)) && ((ToSignedOffset(ToByte(inSrc)) >= 0) == (ToSignedOffset(ToByte(outv)) >= 0));
                } else if (op == 1) {
                    // SUB
                    outv = inDst - inSrc;

                    Context.FlagC = (outv < 0);
                    Context.FlagV = ((ToSignedOffset(ToByte(inDst)) >= 0) != (ToSignedOffset(ToByte(inSrc)) >= 0)) && ((ToSignedOffset(ToByte(inSrc)) >= 0) == (ToSignedOffset(ToByte(outv)) >= 0));
                } else if (op == 2) {
                    // AND
                    outv = inDst & inSrc;
                } else if (op == 3) {
                    // OR
                    outv = inDst | inSrc;
                } else if (op == 4) {
                    // XOR
                    outv = inDst ^ inSrc;
                } else if (op == 5) {
                    // MOV
                    outv = inSrc;
                    Context.FlagV = true;
                }

                byte outb = ToByte(outv);
                
                // Set additional flags
                Context.FlagZ = outb == 0;
                Context.FlagM = (outb & 0x80) == 0x80; 
                
                if (Debug) Console.Write("WRITING " + outb + " TO REG " + regDst + '\n');
                
                // Write register and advance
                Context.SetRegister8(regDst, outb);
                Context.Pc = ToUShort(pc + length);
                
            }
            
            // Word-Wise Double Operand Operation
            else if ((isr & 0xF0) == 0x50) {
                int length = 1;
                int regDst;
                int regSrc;
                bool singlet = false;
                
                // Get register
                if ((isr & 0x08) == 0x08) {
                    regDst = 1;
                    regSrc = 0;
                    singlet = true;
                } else {
                    regSrc = Context.Fetch8(pc + 1) >> 5;
                    regDst = (Context.Fetch8(pc + 1) & 0x0F) >> 1;
                    length = 2;
                }
                
                // Get inputs, outputs, and operation
                int inDst = Context.GetRegister16(regDst);
                int inSrc = Context.GetRegister16(regSrc);
                int outv = 0;
                int op = isr & 0x07;
                
                if (Debug) Console.Write("WORD ALU OP: " + op + " LENGTH: " + length + " SINGLE:" + singlet + "\n");

                if (op == 0) {
                    // ADD
                    outv = inDst + inSrc;

                    Context.FlagC = (outv > 65535);
                    Context.FlagV = ((ToSignedWord(ToUShort(inDst)) >= 0) == (ToSignedWord(ToUShort(inSrc)) >= 0)) && ((ToSignedWord(ToUShort(inSrc)) >= 0) != (ToSignedWord(ToUShort(outv)) >= 0));
                } else if (op == 1) {
                    // SUB
                    outv = inDst - inSrc;

                    Context.FlagC = (outv < 0);
                    Context.FlagV = ((ToSignedWord(ToUShort(inDst)) >= 0) != (ToSignedWord(ToUShort(inSrc)) >= 0)) && ((ToSignedWord(ToUShort(inSrc)) >= 0) == (ToSignedWord(ToUShort(outv)) >= 0));
                } else if (op == 2) {
                    // AND
                    outv = inDst & inSrc;
                } else if (op == 3) {
                    // OR
                    outv = inDst | inSrc;
                } else if (op == 4) {
                    if (singlet) {
                        // MOV DX,AX
                        outv = inSrc;
                        regDst = 3;
                        Context.FlagV = true;
                    } else {
                        // XOR
                        outv = inDst ^ inSrc;
                    }
                } else if (op == 5) {
                    // MOV
                    outv = inSrc;
                    Context.FlagV = true;
                } else if (singlet && op == 6) {
                    // MOV EX,AX
                    outv = inSrc;
                    regDst = 4;
                    Context.FlagV = true;
                }  else if (singlet && op == 7) {
                    // MOV SP,AX
                    outv = inSrc;
                    regDst = 5;
                    Context.FlagV = true;
                }

                ushort outs = ToUShort(outv);
                
                // Set additional flags
                Context.FlagZ = outs == 0;
                Context.FlagM = (outs & 0x8000) == 0x8000; 
                
                // Write register and advance
                Context.SetRegister16(regDst, outs);
                Context.Pc = ToUShort(pc + length);
                
            }
            
            // CX LD/ST Class Instruction
            else if ((isr & 0xF0) == 0x60) {

                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // LD
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;

                    ushort value = Context.Fetch16(addr, false);
                    Context.SetRegister16(2, value);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagV = true;
                    Context.FlagM = (value & 0x8000) == 0x8000; 
                    
                    
                } else {
                    // ST
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;

                    ushort value = Context.GetRegister16(2);
                    Context.Store16(Context.Fetch16(addr, false), value, false);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagV = true;
                    Context.FlagM = (value & 0x8000) == 0x8000; 
                }
                
                Context.Pc = ToUShort(pc + length);
            }


            // Jump/Call Class Instruction
            else if ((isr & 0xF0) == 0x70) {
                ushort destination = 0;

                // Get addressing mode
                int mode = isr & 0x07;
                int length = 1;

                (destination, length) = GetArgumentAddress(mode, pc, false);

                // Call instruction
                if ((isr & 0x08) == 0x08) {
                    
                                        
                    // Push onto the stack
                    _context.SetRegister16(5, ToUShort(_context.GetRegister16(5) - 2));
                    _context.Store16(_context.GetRegister16(5), _context.GetRegister16(2), false);
                    
                    _context.SetRegister16(2, ToUShort(pc + length));
                    _context.FlagL = true;
                }

                Context.Pc = destination;
            }
            
            // AL LD Class Instruction
            else if ((isr & 0xF0) == 0x80) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // LD [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, true);
                    if (op == 0) length = 2;

                    byte value = Context.Fetch8(addr);
                    Context.SetRegister8(1, value);

                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x80) == 0x80; 
                    Context.FlagV = true;
                    
                } else {
                    // LD [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    byte value = Context.Fetch8(addr);
                    Context.SetRegister8(1, value);

                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x80) == 0x80; 
                    Context.FlagV = true;
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // AX LD Class Instruction
            else if ((isr & 0xF0) == 0x90) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // LD [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;
                    
                    ushort value = Context.Fetch16(addr, false);
                    Context.SetRegister16(0, value);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x8000) == 0x8000; 
                    Context.FlagV = true;
                    
                } else {
                    // LD [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    ushort value = Context.Fetch16(addr, false);
                    Context.SetRegister16(0, value);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x8000) == 0x8000; 
                    Context.FlagV = true;
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // AL ST Class Instruction
            else if ((isr & 0xF0) == 0xA0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // ST [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, true);
                    if (op == 0) length = 2;

                    byte value = Context.GetRegister8(1);
                    Context.Store8(addr, value);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x80) == 0x80; 
                    Context.FlagV = true;
                    
                } else {
                    // ST [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    byte value = Context.GetRegister8(1);
                    Context.Store8(addr, value);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x80) == 0x80; 
                    Context.FlagV = true;
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // AX ST Class Instruction
            else if ((isr & 0xF0) == 0xB0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // ST [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;

                    ushort value = Context.GetRegister16(0);
                    Context.Store16(addr, value, false);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x8000) == 0x8000; 
                    Context.FlagV = true;
                    
                } else {
                    // ST [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    ushort value = Context.GetRegister16(0);
                    Context.Store16(addr, value, false);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x8000) == 0x8000; 
                    Context.FlagV = true;
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // BL LD Class Instruction
            else if ((isr & 0xF0) == 0xC0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // LD [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, true);
                    if (op == 0) length = 2;
                    
                    byte value = Context.Fetch8(addr);
                    Context.SetRegister8(3, value);

                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x80) == 0x80; 
                    Context.FlagV = true;
                    
                } else {
                    // LD [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    byte value = Context.Fetch8(addr);
                    Context.SetRegister8(3, value);

                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x80) == 0x80; 
                    Context.FlagV = true;
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // BX LD Class Instruction
            else if ((isr & 0xF0) == 0xD0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // LD [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;
                    
                    ushort value = Context.Fetch16(addr, false);
                    Context.SetRegister16(1, value);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x8000) == 0x8000; 
                    Context.FlagV = true;
                    
                } else {
                    // LD [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    ushort value = Context.Fetch16(addr, false);
                    Context.SetRegister16(1, value);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x8000) == 0x8000; 
                    Context.FlagV = true;
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // BL ST Class Instruction
            else if ((isr & 0xF0) == 0xE0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // ST [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, true);
                    if (op == 0) length = 2;
                    
                    byte value = Context.GetRegister8(3);
                    Context.Store8(addr, value);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x80) == 0x80; 
                    Context.FlagV = true;
                    
                } else {
                    // ST [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    byte value = Context.GetRegister8(3);
                    Context.Store8(addr, value);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x80) == 0x80; 
                    Context.FlagV = true;
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // BX ST Class Instruction
            else if ((isr & 0xF0) == 0xF0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // ST [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;
                    
                    ushort value = Context.GetRegister16(1);
                    Context.Store16(addr, value, false);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x8000) == 0x8000; 
                    Context.FlagV = true;
                    
                } else {
                    // ST [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    ushort value = Context.GetRegister16(1);
                    Context.Store16(addr, value, false);
                    
                    // Set additional flags
                    Context.FlagZ = value == 0;
                    Context.FlagM = (value & 0x8000) == 0x8000; 
                    Context.FlagV = true;
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // Unknown Instruction
            else {
                Context.Pc = ToUShort(pc + 1); 
            }

            return true;
        }
        
        


        /**
         * Gets the address for an address-argument instruction (JUMP, CALL, LD, ST)
         */
        private (ushort, int) GetArgumentAddress(int mode, int pc, bool single) {

            int address;
            int length = 1;

            // Gets the address depending on the mode
            
            switch (mode) {
                case 1:
                    // Absolute addressing
                    address = Context.Fetch16(pc + 1, false);
                    length = 3;
                    break;
                        
                case 2:
                    // Indirect addressing
                    address = Context.Fetch16(Context.Fetch16(pc + 1, false), false);
                    length = 3;
                    break;
                    
                case 3:
                    // PC absolute addressing
                    address = pc + ToSignedOffset(Context.Fetch8(pc + 1)) + 2;
                    length = 2;
                    break;
                    
                case 4:
                    // PC indirect addressing
                    address = Context.Fetch16(ToUShort(pc + ToSignedOffset(Context.Fetch8(pc + 1)) + 2), false);
                    length = 2;
                    break;
                    
                case 5:
                    // Index addressing
                    address = GetIndexedAddr(pc, single);
                    length = 2;
                    if ((Context.Fetch8(pc + 1) & 0x08) == 0x08) length++;
                    break;
                    
                default:
                    address = pc + 1;
                    break;
            }

            if (Debug) Console.WriteLine("MODE: " + mode + " ADDR: " + ToUShort(address) + " LENGTH: " + length);

            return (ToUShort(address), length);
        }

        /**
         * Does addressing for indexed operations
         */
        private ushort GetIndexedAddr(int pc, bool single) {

            // Index mode
            byte imode = Context.Fetch8(pc + 1);
            // Get register
            int reg = (imode % 0xF0) >> 5;

            int address = 0;
            if ((imode & 0x07) == 0x01) {
                // Increment mode
                address = Context.GetRegister16(reg);
                Context.SetRegister16(reg, ToUShort(address + (single ? 1 : 2)));
                
                if (Debug) Console.WriteLine("INDEX INC ON REGISTER " + reg);
            } else if ((imode & 0x07) == 0x02) {
                // Decrement mode
                address = Context.GetRegister16(reg) - (single ? 1 : 2);
                Context.SetRegister16(reg, ToUShort(address));
                
                if (Debug) Console.WriteLine("INDEX INC ON REGISTER " + reg);
            } else {
                // Neutral Mode
                address = Context.GetRegister16(reg);
                
                if (Debug) Console.WriteLine("INDEX NEU ON REGISTER " + reg);
            }

            // Check for offset
            if ((imode & 0x08) == 0x08) {
                address += ToSignedOffset(Context.Fetch8(pc + 2));
            }

            return ToUShort(address);
        }

        /**
         * Turns an int into a UShort with bounding
         */
        private static ushort ToUShort(int i) {
            return Convert.ToUInt16(i & 0xFFFF);
        }

        /**
         * Turns an int into a Byte with bounding
         */
        private static byte ToByte(int i) {
            return Convert.ToByte(i & 0xFF);
        }

        /**
         * Converts an unsigned byte into a signed offset
         */
        private static int ToSignedOffset(byte b) {
            if (b >= 128) return b - 256;
            return b;
        }
        
        /**
         * Converts an unsigned byte into a signed word
         */
        private static int ToSignedWord(ushort w) {
            if (w >= 32768) return w - 65536;
            return w;
        }

        [NotNull]
        public EmulationContext Context {
            get => _context;
            set => _context = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}