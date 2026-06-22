import { defineComponent } from 'vue'

export default defineComponent({
  name: 'SignInHeroPanel',
  props: {
    imageSrc: {
      type: String,
      required: true,
    },
    imageAlt: {
      type: String,
      required: true,
    },
  },
})
