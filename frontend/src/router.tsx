import { createBrowserRouter, Navigate } from 'react-router';
import { RequireAdmin } from './auth/RequireAdmin';
import { Layout } from './components/Layout';
import { BookingPage } from './pages/BookingPage';
import { CartPage } from './pages/CartPage';
import { ErrorPage } from './pages/ErrorPage';
import { HomePage } from './pages/HomePage';
import { HotelPage } from './pages/HotelPage';
import { LoginPage } from './pages/LoginPage';
import { MyBookingsPage } from './pages/MyBookingsPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { AdminCitiesPage } from './pages/admin/AdminCitiesPage';
import { AdminCityFormPage } from './pages/admin/AdminCityFormPage';
import { AdminDealFormPage } from './pages/admin/AdminDealFormPage';
import { AdminHotelFormPage } from './pages/admin/AdminHotelFormPage';
import { AdminRoomFormPage } from './pages/admin/AdminRoomFormPage';
import { AdminUsersPage } from './pages/admin/AdminUsersPage';
import { RegisterPage } from './pages/RegisterPage';
import { SearchPage } from './pages/SearchPage';

// The route table: which URL shows which page. Every page is a child of
// Layout, so they all share the header.
export const router = createBrowserRouter([
  {
    path: '/',
    element: <Layout />,
    errorElement: <ErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'hotels', element: <SearchPage /> },
      { path: 'hotels/:id', element: <HotelPage /> },
      { path: 'cart', element: <CartPage /> },
      { path: 'bookings', element: <MyBookingsPage /> },
      { path: 'bookings/:id', element: <BookingPage /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      {
        // Every admin page sits inside RequireAdmin, so each one doesn't have
        // to check the role itself.
        path: 'admin',
        element: <RequireAdmin />,
        children: [
          { index: true, element: <Navigate to="/admin/cities" replace /> },
          { path: 'cities', element: <AdminCitiesPage /> },
          { path: 'cities/new', element: <AdminCityFormPage /> },
          { path: 'cities/:id', element: <AdminCityFormPage /> },
          { path: 'hotels/new', element: <AdminHotelFormPage /> },
          { path: 'hotels/:id', element: <AdminHotelFormPage /> },
          { path: 'hotels/:hotelId/rooms/new', element: <AdminRoomFormPage /> },
          { path: 'rooms/:id', element: <AdminRoomFormPage /> },
          { path: 'rooms/:roomId/deals/new', element: <AdminDealFormPage /> },
          { path: 'deals/:id', element: <AdminDealFormPage /> },
          { path: 'users', element: <AdminUsersPage /> },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
