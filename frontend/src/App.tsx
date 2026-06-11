import { useEffect, useState } from 'react';
import { getMe } from './api/auth';
import type { UserDto } from './api/auth';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';
import { SuperAdminDashboard } from './pages/SuperAdminDashboard';

function App() {
  const [user, setUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getMe()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return <div className="loading">טוען...</div>;
  }

  if (!user) {
    return <LoginPage onLogin={setUser} />;
  }

  if (user.role === 'SuperAdmin') {
    return <SuperAdminDashboard user={user} onLogout={() => setUser(null)} />;
  }

  return <DashboardPage user={user} onLogout={() => setUser(null)} />;
}

export default App;
