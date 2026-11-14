import { Tabs } from 'expo-router';
import { View } from 'react-native';
import {
  CalendarDays,
  House,
  ListTodo,
  Menu,
  UsersRound,
} from 'lucide-react-native';
import { colors } from '../../src/theme';
import { OfflineBanner } from '../../src/components/OfflineBanner';

export default function TabsLayout() {
  return (
    <View style={{ flex: 1, backgroundColor: colors.bg }}>
      <OfflineBanner />
      <Tabs
        screenOptions={{
          headerStyle: { backgroundColor: colors.bg },
          headerTintColor: colors.text,
          headerTitleStyle: { fontWeight: '700' },
          tabBarStyle: {
            backgroundColor: colors.surface,
            borderTopColor: colors.border,
            borderTopWidth: 1,
            height: 66,
            paddingTop: 7,
            paddingBottom: 7,
          },
          tabBarActiveTintColor: colors.primary,
          tabBarInactiveTintColor: colors.textMuted,
          tabBarLabelStyle: { fontSize: 11, fontWeight: '600', marginTop: 1 },
          tabBarIconStyle: { marginTop: 0 },
          tabBarHideOnKeyboard: true,
        }}
      >
        <Tabs.Screen
          name="index"
          options={{
            title: 'Home',
            tabBarIcon: ({ color, size }) => <House color={color} size={size} strokeWidth={2.1} />,
          }}
        />
        <Tabs.Screen
          name="tasks"
          options={{
            title: 'Tasks',
            tabBarIcon: ({ color, size }) => <ListTodo color={color} size={size} strokeWidth={2.1} />,
          }}
        />
        <Tabs.Screen
          name="calendar"
          options={{
            title: 'Calendar',
            tabBarIcon: ({ color, size }) => <CalendarDays color={color} size={size} strokeWidth={2.1} />,
          }}
        />
        <Tabs.Screen
          name="family"
          options={{
            title: 'Family',
            tabBarIcon: ({ color, size }) => <UsersRound color={color} size={size} strokeWidth={2.1} />,
          }}
        />
        <Tabs.Screen
          name="more"
          options={{
            title: 'More',
            tabBarIcon: ({ color, size }) => <Menu color={color} size={size} strokeWidth={2.1} />,
          }}
        />
        <Tabs.Screen name="requests" options={{ href: null, title: 'Requests' }} />
        <Tabs.Screen name="meals" options={{ href: null, title: 'Meals' }} />
        <Tabs.Screen name="chat" options={{ href: null, title: 'Chat' }} />
        <Tabs.Screen name="settings" options={{ href: null, title: 'Settings' }} />
        <Tabs.Screen name="outcomes" options={{ href: null, title: 'Family outcomes' }} />
      </Tabs>
    </View>
  );
}