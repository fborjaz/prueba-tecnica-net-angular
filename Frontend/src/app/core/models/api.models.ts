/** Formato estándar de respuesta de la API. */
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data?: T;
  errors?: Record<string, string[]>;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export type RoleName = 'ADMINISTRADOR' | 'GESTOR' | 'CAJERO' | 'CLIENTE';

export const ROLES = {
  admin: 'ADMINISTRADOR',
  manager: 'GESTOR',
  cashier: 'CAJERO'
} as const satisfies Record<string, RoleName>;

export interface UserProfile {
  id: number;
  username: string;
  names: string;
  lastNames: string;
  fullName: string;
  email: string;
  identification: string;
  rolId: number;
  role: RoleName;
  statusId: string;
  status: string;
  creationDate: string;
  lastLogin?: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: UserProfile;
}

export interface MenuItem {
  id: number;
  name: string;
  route: string;
  icon: string;
  sortOrder: number;
  children: MenuItem[];
}

export interface CatalogItem {
  id: string;
  description: string;
}

export interface BulkRowError {
  row: number;
  identifier: string;
  messages: string[];
}

export interface BulkUploadResult {
  total: number;
  created: number;
  failed: number;
  errors: BulkRowError[];
}

// ------------------------------------------------------------------ usuarios

export interface User {
  id: number;
  username: string;
  names: string;
  lastNames: string;
  fullName: string;
  email: string;
  identification: string;
  rolId: number;
  role: RoleName;
  statusId: 'ACT' | 'PEN' | 'INA' | 'BLO';
  status: string;
  creationDate: string;
  createdBy?: string;
  approvedBy?: string;
  dateApproval?: string;
  sessionActive: boolean;
  lastLogin?: string;
  failedAttempts: number;
}

export interface SaveUserRequest {
  username?: string;
  names: string;
  lastNames: string;
  email: string;
  identification: string;
  password?: string | null;
  rolId: number;
}

// ------------------------------------------------------------------ clientes

export interface Client {
  id: number;
  name: string;
  lastName: string;
  fullName: string;
  identification: string;
  email: string;
  phoneNumber: string;
  address: string;
  referenceAddress: string;
  active: boolean;
  createdAt: string;
  activeContracts: number;
}

export type SaveClientRequest = Pick<Client,
  'name' | 'lastName' | 'identification' | 'email' | 'phoneNumber' | 'address' | 'referenceAddress'>;

// ------------------------------------------------------------------ cajas

export interface CashUser {
  userId: number;
  username: string;
  fullName: string;
  assignedAt: string;
}

export interface CashSession {
  id: number;
  cashId: number;
  cashDescription: string;
  userId: number;
  username: string;
  openedAt: string;
  closedAt?: string;
}

export interface Cash {
  id: number;
  description: string;
  active: boolean;
  cashiers: CashUser[];
  currentSession?: CashSession;
}

// ------------------------------------------------------------------ turnos

export interface Turn {
  id: number;
  code: string;
  date: string;
  cashId: number;
  cashDescription: string;
  gestorId: number;
  gestorUsername: string;
  attentionTypeId: string;
  attentionType: string;
  attentionId: number;
  statusId: number;
  status: string;
  clientId?: number;
  clientName?: string;
  clientIdentification?: string;
  attendedBy?: string;
  attendedAt?: string;
  notes?: string;
}

export const ATTENTION_STATUS = { pending: 1, inProgress: 2, attended: 3, cancelled: 4 } as const;

// ------------------------------------------------------------------ servicios, contratos, pagos

export interface InternetService {
  id: number;
  name: string;
  description: string;
  speedMbps: number;
  price: number;
  active: boolean;
  devices: string[];
}

export interface MethodPayment {
  id: number;
  description: string;
  active: boolean;
}

export interface Contract {
  id: number;
  startDate: string;
  endDate: string;
  serviceId: number;
  serviceName: string;
  speedMbps: number;
  price: number;
  statusId: 'VIG' | 'REN' | 'SUS' | 'CAN';
  status: string;
  clientId: number;
  clientName: string;
  clientIdentification: string;
  methodPaymentId: number;
  methodPayment: string;
  previousContractId?: number;
  createdAt: string;
}

export interface Payment {
  id: number;
  paymentDate: string;
  clientId: number;
  clientName: string;
  contractId: number;
  serviceName: string;
  methodPayment: string;
  amount: number;
  cashier?: string;
}

// ------------------------------------------------------------------ dashboard

export interface Welcome {
  user: UserProfile;
  attendedTurnsToday: number;
  attendedTurnsScope: string;
  pendingApprovals: User[];
  currentCashSession?: CashSession;
  assignedCashes: Cash[];
}

export interface DashboardIndicators {
  from: string;
  to: string;
  users: {
    totalUsers: number;
    activeUsers: number;
    pendingUsers: number;
    blockedUsers: number;
    inactiveUsers: number;
    activeSessions: number;
    inactiveSessions: number;
    administrators: number;
    managers: number;
    cashiers: number;
  };
  cashes: {
    cashId: number;
    description: string;
    active: boolean;
    assignedCashiers: number;
    currentUser?: string;
    turnsGenerated: number;
    turnsAttended: number;
  }[];
  cashiers: {
    userId: number;
    username: string;
    fullName: string;
    status: string;
    sessionActive: boolean;
    assignedCashes: number;
    turnsAttended: number;
    paymentsAmount: number;
  }[];
  managers: {
    userId: number;
    username: string;
    fullName: string;
    status: string;
    turnsAssigned: number;
    turnsAttended: number;
    usersCreated: number;
    usersPending: number;
  }[];
  daily: { day: string; turnsGenerated: number; turnsAttended: number }[];
}
